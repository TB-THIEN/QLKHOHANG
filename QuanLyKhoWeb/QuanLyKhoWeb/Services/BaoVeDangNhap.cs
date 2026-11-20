using Microsoft.Extensions.Caching.Memory;

namespace QuanLyKhoWeb.Services
{
    // Chống dò mật khẩu / dò mã OTP: đếm số lần sai theo từng tài khoản và từng mã.
    // Bộ đếm nằm trong bộ nhớ (mất khi khởi động lại) - phù hợp với triển khai một máy chủ.
    public class BaoVeDangNhap
    {
        private const int SoLanSaiToiDa = 5;       // sai 5 lần thì khóa tài khoản
        private const int SoLanNhapOtpToiDa = 5;   // nhập sai OTP 5 lần thì hủy mã
        private static readonly TimeSpan ThoiGianKhoa = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CuaSoDem = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ThoiGianLuu = TimeSpan.FromHours(1);

        private sealed class BoDem
        {
            public int SoLan;
            public DateTime LanCuoi;
            public DateTime? KhoaDen;
        }

        private readonly IMemoryCache _cache;
        public BaoVeDangNhap(IMemoryCache cache) => _cache = cache;

        private static string KhoaDangNhap(string tenDangNhap)
            => "dangnhap:" + tenDangNhap.Trim().ToLowerInvariant();

        // Trả về thời gian còn bị khóa; TimeSpan.Zero nếu không bị khóa.
        public TimeSpan LayThoiGianKhoa(string tenDangNhap)
        {
            if (string.IsNullOrWhiteSpace(tenDangNhap)) return TimeSpan.Zero;
            if (!_cache.TryGetValue(KhoaDangNhap(tenDangNhap), out BoDem? boDem) || boDem?.KhoaDen == null)
                return TimeSpan.Zero;

            var conLai = boDem.KhoaDen.Value - DateTime.Now;
            return conLai > TimeSpan.Zero ? conLai : TimeSpan.Zero;
        }

        public void GhiNhanSai(string tenDangNhap)
        {
            if (string.IsNullOrWhiteSpace(tenDangNhap)) return;

            var khoa = KhoaDangNhap(tenDangNhap);
            var boDem = _cache.Get<BoDem>(khoa) ?? new BoDem();
            var bayGio = DateTime.Now;

            // Sai rải rác quá lâu thì đếm lại từ đầu
            if (bayGio - boDem.LanCuoi > CuaSoDem) boDem.SoLan = 0;

            boDem.SoLan++;
            boDem.LanCuoi = bayGio;
            if (boDem.SoLan >= SoLanSaiToiDa) boDem.KhoaDen = bayGio + ThoiGianKhoa;

            _cache.Set(khoa, boDem, bayGio + ThoiGianLuu);
        }

        public void XoaTheoDoi(string tenDangNhap)
        {
            if (string.IsNullOrWhiteSpace(tenDangNhap)) return;
            _cache.Remove(KhoaDangNhap(tenDangNhap));
        }

        // Đếm số lần nhập sai OTP của một mã; trả về true khi đã vượt giới hạn.
        public bool VuotQuaGioiHanOtpSai(int maDatLai)
        {
            if (maDatLai <= 0) return false;

            var khoa = "otp-sai:" + maDatLai;
            var soLan = (_cache.Get<int?>(khoa) ?? 0) + 1;
            _cache.Set(khoa, soLan, DateTime.Now + ThoiGianLuu);
            return soLan > SoLanNhapOtpToiDa;
        }

        public void XoaBoDemOtp(int maDatLai)
        {
            if (maDatLai > 0) _cache.Remove("otp-sai:" + maDatLai);
        }
    }
}
