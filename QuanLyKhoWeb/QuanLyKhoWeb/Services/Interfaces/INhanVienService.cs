namespace QuanLyKhoWeb.Services.Interfaces
{
    // Dữ liệu để tạo một nhân viên mới.
    // Dùng chung cho form Thêm nhân viên (NhanVienController) và trợ lý AI,
    // nên KHÔNG chứa IFormFile: ảnh thẻ do nơi gọi lưu file rồi truyền đường dẫn vào AnhThe.
    public class NhanVienTaoMoi
    {
        public string TenDangNhap { get; set; } = string.Empty;
        public string MatKhau { get; set; } = string.Empty;
        public string HoTen { get; set; } = string.Empty;
        public string TenVaiTro { get; set; } = string.Empty;

        public string? Email { get; set; }
        public string? SoDienThoai { get; set; }
        public DateOnly? NgaySinh { get; set; }
        public string? GioiTinh { get; set; }
        public string? ThuongTru { get; set; }
        public string? Cccd { get; set; }
        public string? AnhThe { get; set; }
        public string? StkNganHang { get; set; }
        public string? ChiNhanhNganHang { get; set; }
        public bool TrangThaiHoatDong { get; set; } = true;

        // Chỉ áp dụng khi SP tạo vai trò mới; vai trò đã có giữ nguyên cờ cũ.
        public bool GiaoHang { get; set; }
    }

    // Một lỗi gắn với tên trường để controller đưa thẳng vào ModelState
    // (truong = "" là lỗi chung, hiện ở phần tóm tắt).
    public record LoiTaoNhanVien(string Truong, string ThongBao);

    public class KetQuaTaoNhanVien
    {
        public bool ThanhCong { get; init; }
        public int? MaNguoiDung { get; init; }
        public string? TenDangNhap { get; init; }
        public IReadOnlyList<LoiTaoNhanVien> Loi { get; init; } = Array.Empty<LoiTaoNhanVien>();
    }

    public interface INhanVienService
    {
        // Kiểm tra dữ liệu mà không ghi gì vào cơ sở dữ liệu.
        // Trợ lý AI dùng để báo lỗi ngay lúc lập bản nháp.
        Task<IReadOnlyList<LoiTaoNhanVien>> KiemTraAsync(NhanVienTaoMoi yeuCau, CancellationToken ct = default);

        // Kiểm tra lại lần cuối rồi gọi sp_TaoNhanVien.
        Task<KetQuaTaoNhanVien> TaoAsync(NhanVienTaoMoi yeuCau, CancellationToken ct = default);
    }
}
