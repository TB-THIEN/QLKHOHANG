using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using QuanLyKhoWeb.Services.Interfaces;
using System.Security.Cryptography;

namespace QuanLyKhoWeb.Services.Ai
{
    // Một thao tác ghi mà trợ lý đã chuẩn bị nhưng CHƯA thực hiện.
    // Người dùng phải bấm "Xác nhận" thì mới gọi service ghi dữ liệu.
    public class HanhDongCho
    {
        public string Id { get; init; } = string.Empty;
        public int MaNguoiDung { get; init; }
        public string Loai { get; init; } = string.Empty;
        public string TieuDe { get; init; } = string.Empty;

        // Dữ liệu đã được kiểm tra hợp lệ, sẵn sàng ghi khi người dùng xác nhận.
        public NhanVienTaoMoi? NhanVien { get; init; }

        // Các dòng tóm tắt để người dùng đọc trước khi xác nhận (nhãn, giá trị).
        public List<KeyValuePair<string, string>> TomTat { get; init; } = new();

        public DateTime HetHan { get; init; }
    }

    // Giữ "hành động chờ" trong bộ nhớ đệm của tiến trình.
    // Khóa luôn gắn với mã người dùng nên người khác không xác nhận hộ được.
    public class KhoHanhDongCho
    {
        private readonly IMemoryCache _cache;
        private readonly AiCauHinh _cauHinh;

        public KhoHanhDongCho(IMemoryCache cache, IOptions<AiCauHinh> cauHinh)
        {
            _cache = cache;
            _cauHinh = cauHinh.Value;
        }

        public HanhDongCho Them(int maNguoiDung, string loai, string tieuDe,
            NhanVienTaoMoi? nhanVien, IEnumerable<KeyValuePair<string, string>> tomTat)
        {
            // 12 byte ngẫu nhiên -> 24 ký tự hex, đủ khó đoán để không ai dò được id của người khác.
            var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
            var han = DateTime.UtcNow.AddMinutes(Math.Max(1, _cauHinh.PhutChoXacNhan));

            var hanhDong = new HanhDongCho
            {
                Id = id,
                MaNguoiDung = maNguoiDung,
                Loai = loai,
                TieuDe = tieuDe,
                NhanVien = nhanVien,
                TomTat = tomTat.ToList(),
                HetHan = han
            };

            _cache.Set(Khoa(maNguoiDung, id), hanhDong, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(1, _cauHinh.PhutChoXacNhan))
            });
            return hanhDong;
        }

        // Chỉ trả về khi id tồn tại VÀ thuộc đúng người dùng đang đăng nhập.
        public HanhDongCho? Lay(int maNguoiDung, string id)
            => string.IsNullOrWhiteSpace(id) ? null : _cache.Get<HanhDongCho>(Khoa(maNguoiDung, id));

        // Xoá sau khi dùng để một xác nhận không thể chạy hai lần.
        public void Xoa(int maNguoiDung, string id) => _cache.Remove(Khoa(maNguoiDung, id));

        private static string Khoa(int maNguoiDung, string id) => $"ai-hanh-dong:{maNguoiDung}:{id}";
    }
}
