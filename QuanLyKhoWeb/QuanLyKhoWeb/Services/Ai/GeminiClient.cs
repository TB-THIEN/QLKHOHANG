using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuanLyKhoWeb.Services.Ai
{
    // Một lượt gọi model trả về: có thể là câu trả lời bằng chữ, hoặc lời gọi công cụ,
    // hoặc cả hai. Parts giữ nguyên JSON gốc (kể cả thoughtSignature) để gửi lại ở vòng sau.
    public class KetQuaGoiModel
    {
        public bool ThanhCong { get; init; }
        public string? Loi { get; init; }
        public string? VanBan { get; init; }
        public List<LoiGoiCongCu> GoiCongCu { get; init; } = new();

        // Nội dung "model" nguyên bản (giữ thoughtSignature) để nối vào lịch sử hội thoại.
        public JsonArray? PartsGoc { get; init; }
    }

    public class LoiGoiCongCu
    {
        public string Ten { get; init; } = string.Empty;
        public JsonObject ThamSo { get; init; } = new();
        public string? Id { get; init; }
    }

    // Gọi Gemini qua API gốc (v1beta/models/{model}:generateContent).
    // Không dùng endpoint "OpenAI-compatible" vì loại khóa API hiện tại trả 404 ở đó.
    public class GeminiClient
    {
        private readonly HttpClient _http;
        private readonly AiCauHinh _cauHinh;

        public GeminiClient(HttpClient http, IOptions<AiCauHinh> cauHinh)
        {
            _http = http;
            _cauHinh = cauHinh.Value;
        }

        public async Task<KetQuaGoiModel> GoiAsync(
            string chiDanHeThong,
            JsonArray lichSu,
            JsonArray? khaiBaoCongCu,
            CancellationToken ct = default)
        {
            var thanRequest = new JsonObject
            {
                ["systemInstruction"] = new JsonObject
                {
                    ["parts"] = new JsonArray(new JsonObject { ["text"] = chiDanHeThong })
                },
                // Clone: JsonNode không cho gán cùng một node vào hai cha, mà vòng lặp
                // gọi lại GoiAsync nhiều lần với cùng mảng lichSu.
                ["contents"] = lichSu.DeepClone(),
                ["generationConfig"] = new JsonObject
                {
                    ["maxOutputTokens"] = _cauHinh.MaxTokens,
                    ["temperature"] = 0.2
                }
            };
            if (khaiBaoCongCu != null && khaiBaoCongCu.Count > 0)
            {
                thanRequest["tools"] = new JsonArray(new JsonObject
                {
                    ["functionDeclarations"] = khaiBaoCongCu.DeepClone()
                });
            }

            var url = $"{_cauHinh.BaseUrl.TrimEnd('/')}/models/{Uri.EscapeDataString(_cauHinh.Model)}:generateContent";

            // Gemini đôi khi trả 503 khi model đang quá tải -> thử lại 2 lần là đủ cho một thao tác UI.
            for (var lanThu = 1; lanThu <= 2; lanThu++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(thanRequest.ToJsonString(), Encoding.UTF8, "application/json")
                };
                request.Headers.TryAddWithoutValidation("x-goog-api-key", _cauHinh.ApiKey);

                try
                {
                    using var response = await _http.SendAsync(request, ct);
                    var noiDung = await response.Content.ReadAsStringAsync(ct);

                    if (response.StatusCode == HttpStatusCode.ServiceUnavailable && lanThu < 2)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), ct);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                        return new KetQuaGoiModel { ThanhCong = false, Loi = DocLoi(noiDung, response.StatusCode) };

                    return PhanTich(noiDung);
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested)
                {
                    return new KetQuaGoiModel { ThanhCong = false, Loi = "Máy chủ AI phản hồi quá lâu. Vui lòng thử lại." };
                }
                catch (HttpRequestException ex)
                {
                    return new KetQuaGoiModel { ThanhCong = false, Loi = "Không kết nối được tới dịch vụ AI: " + ex.Message };
                }
            }

            return new KetQuaGoiModel { ThanhCong = false, Loi = "Dịch vụ AI đang quá tải. Vui lòng thử lại sau ít phút." };
        }

        private static KetQuaGoiModel PhanTich(string json)
        {
            JsonNode? goc;
            try { goc = JsonNode.Parse(json); }
            catch (JsonException) { return new KetQuaGoiModel { ThanhCong = false, Loi = "Phản hồi AI không đọc được." }; }

            var ungVien = goc?["candidates"]?.AsArray().FirstOrDefault();
            var parts = ungVien?["content"]?["parts"]?.AsArray();
            if (parts == null || parts.Count == 0)
            {
                var lyDo = ungVien?["finishReason"]?.GetValue<string>();
                var thongBao = lyDo switch
                {
                    "SAFETY" => "Nội dung bị bộ lọc an toàn của AI từ chối.",
                    "MAX_TOKENS" => "Câu trả lời quá dài nên bị cắt. Vui lòng hỏi ngắn gọn hơn.",
                    _ => "AI không trả về nội dung nào. Vui lòng thử lại."
                };
                return new KetQuaGoiModel { ThanhCong = false, Loi = thongBao };
            }

            var vanBan = new StringBuilder();
            var danhSachGoi = new List<LoiGoiCongCu>();

            foreach (var part in parts)
            {
                var chu = part?["text"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(chu)) vanBan.Append(chu);

                var goi = part?["functionCall"];
                if (goi != null)
                {
                    danhSachGoi.Add(new LoiGoiCongCu
                    {
                        Ten = goi["name"]?.GetValue<string>() ?? string.Empty,
                        ThamSo = goi["args"] as JsonObject ?? new JsonObject(),
                        Id = goi["id"]?.GetValue<string>()
                    });
                }
            }

            return new KetQuaGoiModel
            {
                ThanhCong = true,
                VanBan = vanBan.Length > 0 ? vanBan.ToString() : null,
                GoiCongCu = danhSachGoi,
                PartsGoc = parts.DeepClone().AsArray()
            };
        }

        private static string DocLoi(string json, HttpStatusCode status)
        {
            try
            {
                var loi = JsonNode.Parse(json)?["error"];
                var thongBao = loi?["message"]?.GetValue<string>();
                var maLoi = loi?["status"]?.GetValue<string>();
                if (maLoi == "RESOURCE_EXHAUSTED" || (int)status == 429)
                    return "Đã hết hạn mức gọi AI. Vui lòng thử lại sau.";
                if (maLoi == "UNAVAILABLE")
                    return "Dịch vụ AI đang quá tải. Vui lòng thử lại sau ít phút.";
                if (!string.IsNullOrWhiteSpace(thongBao))
                    return $"Lỗi từ dịch vụ AI: {thongBao}";
            }
            catch (JsonException) { /* rơi xuống thông báo mặc định bên dưới */ }

            // Không lộ chi tiết kỹ thuật ra UI, chỉ ghi mã trạng thái để dễ tra log.
            return $"Dịch vụ AI trả về lỗi ({(int)status}). Vui lòng thử lại.";
        }
    }
}
