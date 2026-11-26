using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Models.Entities;
using QuanLyKhoWeb.Services.Interfaces;
using System.Data;

namespace QuanLyKhoWeb.Services
{
    // Một chỗ duy nhất để tạo nhân viên: form Thêm nhân viên và trợ lý AI đều gọi vào đây,
    // nhờ vậy hai đường không bị lệch quy tắc kiểm tra.
    // Phần ghi vẫn đi qua sp_TaoNhanVien như cũ (SP kiểm tra lại lần cuối).
    public class NhanVienService : INhanVienService
    {
        private readonly DoAnQlkContext _context;
        private readonly IDbConnectionFactory _factory;

        public NhanVienService(DoAnQlkContext context, IDbConnectionFactory factory)
        {
            _context = context;
            _factory = factory;
        }

        public async Task<IReadOnlyList<LoiTaoNhanVien>> KiemTraAsync(NhanVienTaoMoi yeuCau, CancellationToken ct = default)
        {
            var loi = new List<LoiTaoNhanVien>();

            var tenDangNhap = (yeuCau.TenDangNhap ?? string.Empty).Trim();
            var hoTen = (yeuCau.HoTen ?? string.Empty).Trim();
            var vaiTro = (yeuCau.TenVaiTro ?? string.Empty).Trim();
            var email = ChuanHoa(yeuCau.Email);
            var cccd = ChuanHoa(yeuCau.Cccd);

            if (string.IsNullOrWhiteSpace(tenDangNhap))
                loi.Add(new LoiTaoNhanVien(nameof(NhanVienTaoMoi.TenDangNhap), "Vui lòng nhập tên đăng nhập."));
            else if (tenDangNhap.Length > 50)
                loi.Add(new LoiTaoNhanVien(nameof(NhanVienTaoMoi.TenDangNhap), "Tên đăng nhập tối đa 50 ký tự."));
            else if (await _context.NguoiDungs.AnyAsync(n => n.TenDangNhap == tenDangNhap, ct))
                loi.Add(new LoiTaoNhanVien(nameof(NhanVienTaoMoi.TenDangNhap),
                    "Tên đăng nhập đã tồn tại, vui lòng chọn tên khác."));

            if (string.IsNullOrWhiteSpace(hoTen))
                loi.Add(new LoiTaoNhanVien(nameof(NhanVienTaoMoi.HoTen), "Vui lòng nhập họ tên."));
            else if (!KiemTraDauVao.LaTenHopLe(hoTen))
                loi.Add(new LoiTaoNhanVien(nameof(NhanVienTaoMoi.HoTen), "Họ tên không được chứa số."));
            else if (hoTen.Length > 100)
                loi.Add(new LoiTaoNhanVien(nameof(NhanVienTaoMoi.HoTen), "Họ tên tối đa 100 ký tự."));

            if (string.IsNullOrWhiteSpace(vaiTro))
                loi.Add(new LoiTaoNhanVien(nameof(NhanVienTaoMoi.TenVaiTro), "Vui lòng chọn hoặc nhập vai trò."));
            else if (vaiTro.Length > 100)
                loi.Add(new LoiTaoNhanVien(nameof(NhanVienTaoMoi.TenVaiTro), "Tên vai trò tối đa 100 ký tự."));

            if (string.IsNullOrWhiteSpace(yeuCau.MatKhau) || yeuCau.MatKhau.Length < 6)
                loi.Add(new LoiTaoNhanVien(nameof(NhanVienTaoMoi.MatKhau), "Mật khẩu tối thiểu 6 ký tự."));

            if (email != null)
            {
                if (!email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase)
                    || email.Contains(' ') || email.Count(c => c == '@') != 1)
                    loi.Add(new LoiTaoNhanVien(nameof(NhanVienTaoMoi.Email), "Email phải có đuôi @gmail.com."));
                else if (await _context.NguoiDungs.AnyAsync(n => n.Email == email, ct))
                    loi.Add(new LoiTaoNhanVien(nameof(NhanVienTaoMoi.Email), "Email này đã được sử dụng."));
            }

            if (cccd != null)
            {
                if (cccd.Length != 12 || !cccd.All(char.IsAsciiDigit))
                    loi.Add(new LoiTaoNhanVien(nameof(NhanVienTaoMoi.Cccd), "CCCD phải gồm đúng 12 chữ số."));
                else if (await _context.NguoiDungs.AnyAsync(n => n.Cccd == cccd, ct))
                    loi.Add(new LoiTaoNhanVien(nameof(NhanVienTaoMoi.Cccd), "CCCD này đã được sử dụng."));
            }

            return loi;
        }

        public async Task<KetQuaTaoNhanVien> TaoAsync(NhanVienTaoMoi yeuCau, CancellationToken ct = default)
        {
            var loi = await KiemTraAsync(yeuCau, ct);
            if (loi.Count > 0)
                return new KetQuaTaoNhanVien { ThanhCong = false, Loi = loi };

            var tenDangNhap = yeuCau.TenDangNhap.Trim();
            var email = ChuanHoa(yeuCau.Email);
            var cccd = ChuanHoa(yeuCau.Cccd);

            try
            {
                using var conn = (SqlConnection)_factory.CreateConnection();
                var p = new DynamicParameters();
                p.Add("@ten_dang_nhap", tenDangNhap);
                p.Add("@mat_khau_ma_hoa", BCrypt.Net.BCrypt.HashPassword(yeuCau.MatKhau));
                p.Add("@ho_ten", yeuCau.HoTen.Trim());
                p.Add("@vai_tro", yeuCau.TenVaiTro.Trim());
                p.Add("@email", email);
                p.Add("@so_dien_thoai", ChuanHoa(yeuCau.SoDienThoai));
                p.Add("@ngay_sinh", yeuCau.NgaySinh);
                p.Add("@gioi_tinh", ChuanHoa(yeuCau.GioiTinh));
                p.Add("@thuong_tru", ChuanHoa(yeuCau.ThuongTru));
                p.Add("@cccd", cccd);
                p.Add("@anh_the", ChuanHoa(yeuCau.AnhThe));
                p.Add("@stk_ngan_hang", ChuanHoa(yeuCau.StkNganHang));
                p.Add("@chi_nhanh_ngan_hang", ChuanHoa(yeuCau.ChiNhanhNganHang));
                // Chỉ áp dụng khi vai trò CHƯA tồn tại (SP tự tạo vai trò mới với 2 quyền này).
                p.Add("@co_quyen_huy_don", false);
                p.Add("@giao_hang", yeuCau.GiaoHang);
                await conn.OpenAsync(ct);
                await conn.ExecuteAsync(new CommandDefinition(
                    "sp_TaoNhanVien", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));
            }
            catch (SqlException ex)
            {
                // Các RAISERROR trong SP (trùng tên đăng nhập/email, email sai đuôi, quá 1 quản lý...)
                return new KetQuaTaoNhanVien
                {
                    ThanhCong = false,
                    Loi = new[] { new LoiTaoNhanVien(string.Empty, ex.Message) }
                };
            }

            // sp_TaoNhanVien luôn tạo tài khoản ở trạng thái "đang làm"; nếu chọn "Nghỉ" ngay lúc
            // tạo thì cập nhật lại bằng EF (SP không nhận tham số này).
            if (!yeuCau.TrangThaiHoatDong)
            {
                var nvMoi = await _context.NguoiDungs.FirstOrDefaultAsync(n => n.TenDangNhap == tenDangNhap, ct);
                if (nvMoi != null)
                {
                    nvMoi.TrangThaiHoatDong = false;
                    await _context.SaveChangesAsync(ct);
                }
            }

            var maNguoiDung = await _context.NguoiDungs
                .Where(n => n.TenDangNhap == tenDangNhap)
                .Select(n => (int?)n.MaNguoiDung)
                .FirstOrDefaultAsync(ct);

            return new KetQuaTaoNhanVien
            {
                ThanhCong = true,
                MaNguoiDung = maNguoiDung,
                TenDangNhap = tenDangNhap
            };
        }

        private static string? ChuanHoa(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
