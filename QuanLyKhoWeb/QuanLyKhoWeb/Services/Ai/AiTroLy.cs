using Microsoft.Extensions.Options;
using QuanLyKhoWeb.Services.Interfaces;
using System.Text;
using System.Text.Json.Nodes;

namespace QuanLyKhoWeb.Services.Ai
{
    public class LuotHoiThoai
    {
        public string VaiTro { get; set; } = "nguoi_dung";
        public string NoiDung { get; set; } = string.Empty;
    }

    public class KetQuaTraLoi
    {
        public bool ThanhCong { get; init; }
        public string? TraLoi { get; init; }
        public string? Loi { get; init; }
        public HanhDongCho? HanhDong { get; init; }
    }

    // Bộ điều phối: dựng chỉ dẫn, chạy vòng lặp model -> tool -> model,
    // và dừng ngay khi có một hành động ghi đang chờ xác nhận.
    public class AiTroLy
    {
        private readonly GeminiClient _gemini;
        private readonly KhoCongCu _congCu;
        private readonly AiCauHinh _cauHinh;

        public AiTroLy(GeminiClient gemini, KhoCongCu congCu, IOptions<AiCauHinh> cauHinh)
        {
            _gemini = gemini;
            _congCu = congCu;
            _cauHinh = cauHinh.Value;
        }

        private const string ChiDan = """
            Bạn là trợ lý nội bộ của ứng dụng quản lý kho "Sổ Kho", nói tiếng Việt, ngắn gọn và lịch sự.

            Nhiệm vụ hiện tại: giúp quản lý THÊM NHÂN VIÊN MỚI và tra cứu nhân viên.

            Quy tắc bắt buộc:
            1. Chỉ dùng thông tin người dùng cung cấp và dữ liệu trả về từ công cụ. Tuyệt đối không bịa
               họ tên, tên đăng nhập, mật khẩu, email, số điện thoại, CCCD hay bất kỳ thông tin cá nhân nào.
            2. Nếu thiếu họ tên, tên đăng nhập, mật khẩu hoặc vai trò thì phải hỏi lại, không tự đoán.
            3. Mật khẩu là do quản lý tự đặt và tự gõ vào. Bạn TUYỆT ĐỐI không được tự sinh, tự nghĩ ra,
               hay gợi ý một mật khẩu cụ thể. Không có mật khẩu thì không được gọi tao_nhan_vien.
               Mật khẩu phải có ít nhất 6 ký tự; nếu ngắn hơn thì báo để quản lý nhập lại.
               Không hỏi lại mật khẩu lần hai khi người dùng đã cung cấp, và không nhắc lại mật khẩu
               trong câu trả lời của mình.
            4. Trước khi tạo, nếu người dùng đưa tên đăng nhập hoặc CCCD thì nên kiểm tra trùng bằng công cụ
               tim_nhan_vien. Nếu cần biết vai trò chính xác, dùng liet_ke_vai_tro.
            5. Khi đã đủ dữ liệu hợp lệ, gọi tao_nhan_vien. Công cụ này chỉ tạo BẢN NHÁP, chưa ghi gì.
               Sau đó nói rõ: bản nháp đang chờ quản lý bấm "Xác nhận" để hoàn tất.
            6. Nếu công cụ trả về lỗi dữ liệu, giải thích lỗi bằng lời dễ hiểu và đề nghị người dùng sửa.
            7. Không hứa hẹn việc gì ngoài khả năng của các công cụ được cấp. Không bàn về chủ đề khác.
            8. Không hiển thị HTML, không dùng markdown phức tạp; chỉ văn bản thường và gạch đầu dòng "- ".
            """;

        public async Task<KetQuaTraLoi> HoiAsync(
            int maNguoiDung, string tinNhan, IEnumerable<LuotHoiThoai>? lichSu, CancellationToken ct = default)
        {
            if (!_cauHinh.DaCauHinh)
            {
                return new KetQuaTraLoi
                {
                    ThanhCong = false,
                    Loi = "Trợ lý AI chưa được cấu hình khóa API. "
                        + "Quản trị viên cần chạy: dotnet user-secrets set \"Ai:ApiKey\" \"<khóa>\"."
                };
            }

            var lichSuJson = DungLichSu(lichSu, tinNhan);
            var khaiBao = _congCu.KhaiBao();
            var ghiChuCongCu = new StringBuilder();

            // Toàn bộ lời của người dùng trong phiên (gồm tin nhắn hiện tại). Dùng để đối chiếu
            // mật khẩu: chỉ chấp nhận mật khẩu mà chính quản lý đã gõ ra, không chấp nhận giá trị
            // do model tự sinh. Gộp cả lịch sử vì quản lý có thể đưa mật khẩu ở lượt trước.
            var loiNguoiDung = new StringBuilder(tinNhan);
            if (lichSu != null)
            {
                foreach (var luot in lichSu.Where(x => x.VaiTro != "tro_ly"))
                    loiNguoiDung.Append('\n').Append(luot.NoiDung);
            }
            var vanBanNguoiDung = loiNguoiDung.ToString();

            for (var vong = 1; vong <= Math.Max(1, _cauHinh.SoVongToiDa); vong++)
            {
                var ketQua = await _gemini.GoiAsync(ChiDan, lichSuJson, khaiBao, ct);
                if (!ketQua.ThanhCong)
                    return new KetQuaTraLoi { ThanhCong = false, Loi = ketQua.Loi };

                // Không có lời gọi công cụ -> đây là câu trả lời cuối cùng.
                if (ketQua.GoiCongCu.Count == 0 || ketQua.PartsGoc == null)
                {
                    var traLoi = ketQua.VanBan;
                    if (string.IsNullOrWhiteSpace(traLoi))
                        traLoi = "Mình chưa có câu trả lời cho yêu cầu này. Bạn nói rõ hơn giúp mình nhé.";
                    if (ghiChuCongCu.Length > 0)
                        traLoi = ghiChuCongCu + "\n\n" + traLoi;
                    return new KetQuaTraLoi { ThanhCong = true, TraLoi = traLoi.Trim() };
                }

                // Phải gửi lại nguyên parts của model (kèm thoughtSignature), nếu thiếu Gemini trả 400.
                lichSuJson.Add(new JsonObject { ["role"] = "model", ["parts"] = ketQua.PartsGoc });

                var phanHoi = new JsonArray();
                HanhDongCho? hanhDong = null;

                foreach (var goi in ketQua.GoiCongCu)
                {
                    var kq = await _congCu.ThucThiAsync(maNguoiDung, goi.Ten, goi.ThamSo, vanBanNguoiDung, ct);
                    phanHoi.Add(new JsonObject
                    {
                        ["functionResponse"] = new JsonObject
                        {
                            ["name"] = goi.Ten,
                            ["response"] = kq.DuLieu
                        }
                    });

                    if (kq.HanhDong != null)
                    {
                        hanhDong = kq.HanhDong;
                        break;
                    }
                }

                lichSuJson.Add(new JsonObject { ["role"] = "user", ["parts"] = phanHoi });

                if (hanhDong != null)
                {
                    var loiNhac = string.IsNullOrWhiteSpace(ketQua.VanBan)
                        ? $"Mình đã chuẩn bị bản nháp: {hanhDong.TieuDe}. Bạn kiểm tra rồi bấm Xác nhận để hoàn tất."
                        : ketQua.VanBan!.Trim();

                    return new KetQuaTraLoi
                    {
                        ThanhCong = true,
                        TraLoi = loiNhac,
                        HanhDong = hanhDong
                    };
                }
            }

            return new KetQuaTraLoi
            {
                ThanhCong = true,
                TraLoi = "Yêu cầu này cần nhiều bước quá nên mình dừng lại cho an toàn. "
                    + "Bạn chia nhỏ yêu cầu giúp mình nhé."
            };
        }

        // Lịch sử do trình duyệt gửi lên chỉ gồm văn bản (không có tool), nên dựng lại đơn giản.
        // Chỉ giữ 10 lượt gần nhất để kiểm soát chi phí token.
        private static JsonArray DungLichSu(IEnumerable<LuotHoiThoai>? lichSu, string tinNhan)
        {
            var mang = new JsonArray();
            if (lichSu != null)
            {
                foreach (var luot in lichSu.Where(x => !string.IsNullOrWhiteSpace(x.NoiDung)).TakeLast(10))
                {
                    mang.Add(new JsonObject
                    {
                        ["role"] = luot.VaiTro == "tro_ly" ? "model" : "user",
                        ["parts"] = new JsonArray(new JsonObject { ["text"] = luot.NoiDung })
                    });
                }
            }

            mang.Add(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = tinNhan })
            });
            return mang;
        }
    }
}
