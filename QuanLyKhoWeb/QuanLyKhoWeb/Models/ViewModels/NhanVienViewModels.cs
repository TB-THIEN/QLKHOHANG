using System.ComponentModel.DataAnnotations;

namespace QuanLyKhoWeb.Models.ViewModels
{
    public class NhanVienCreateVm
    {
        [Required(ErrorMessage = "Vui lòng nhập tên nhân viên")]
        [Display(Name = "Tên nhân viên")]
        public string HoTen { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [Display(Name = "Tên đăng nhập")]
        public string TenDangNhap { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
        [Display(Name = "Mật khẩu")]
        public string MatKhau { get; set; } = string.Empty;

        // Nhập tay vai trò mới hoặc chọn vai trò có sẵn (autocomplete)
        [Required(ErrorMessage = "Vui lòng nhập/chọn vai trò")]
        [Display(Name = "Vai trò")]
        public string TenVaiTro { get; set; } = string.Empty;

        public string? SoDienThoai { get; set; }
        public string? Email { get; set; }
        public string? ThuongTru { get; set; }
        public DateOnly? NgaySinh { get; set; }
        public string? GioiTinh { get; set; }

        [RegularExpression(@"^\d{12}$", ErrorMessage = "CCCD phải gồm đúng 12 chữ số.")]
        [Display(Name = "CCCD (12 số)")]
        public string? Cccd { get; set; }

        [Display(Name = "Ảnh thẻ 3x4")]
        public IFormFile? AnhThe { get; set; }

        [Display(Name = "STK ngân hàng")]
        public string? StkNganHang { get; set; }

        [Display(Name = "Chi nhánh ngân hàng")]
        public string? ChiNhanhNganHang { get; set; }

        [Display(Name = "Trạng thái")]
        public bool TrangThaiHoatDong { get; set; } = true; // true=đang làm, false=nghỉ làm (ẩn khỏi danh sách)
    }

    public class NhanVienEditVm
    {
        public int MaNguoiDung { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string HoTen { get; set; } = string.Empty;
        public string? SoDienThoai { get; set; }
        public string? Email { get; set; }
        public string? ThuongTru { get; set; }
        public DateOnly? NgaySinh { get; set; }
        public string? GioiTinh { get; set; }

        [RegularExpression(@"^\d{12}$", ErrorMessage = "CCCD phải gồm đúng 12 chữ số.")]
        [Display(Name = "CCCD (12 số)")]
        public string? Cccd { get; set; }

        public string? AnhTheHienTai { get; set; }

        [Display(Name = "Đổi ảnh thẻ 3x4")]
        public IFormFile? AnhTheMoi { get; set; }

        [Display(Name = "STK ngân hàng")]
        public string? StkNganHang { get; set; }

        [Display(Name = "Chi nhánh ngân hàng")]
        public string? ChiNhanhNganHang { get; set; }
    }

    public class DoiMatKhauVm
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string MatKhauHienTai { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới")]
        [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
        public string MatKhauMoi { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới")]
        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu mới")]
        [Compare("MatKhauMoi", ErrorMessage = "Mật khẩu nhập lại không khớp")]
        public string XacNhanMatKhau { get; set; } = string.Empty;
    }

    public class ThongTinCaNhanVm
    {
        public int MaNguoiDung { get; set; }
        public string TenDangNhap { get; set; } = string.Empty;
        public string TenVaiTro { get; set; } = string.Empty;
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string HoTen { get; set; } = string.Empty;
        public string? SoDienThoai { get; set; }
        public string? Email { get; set; }
        public string? ThuongTru { get; set; }
        public DateOnly? NgaySinh { get; set; }
        public string? GioiTinh { get; set; }

        [RegularExpression(@"^\d{12}$", ErrorMessage = "CCCD phải gồm đúng 12 chữ số.")]
        [Display(Name = "CCCD (12 số)")]
        public string? Cccd { get; set; }

        public string? AnhTheHienTai { get; set; }

        [Display(Name = "Đổi ảnh thẻ 3x4")]
        public IFormFile? AnhTheMoi { get; set; }

        [Display(Name = "STK ngân hàng")]
        public string? StkNganHang { get; set; }

        [Display(Name = "Chi nhánh ngân hàng")]
        public string? ChiNhanhNganHang { get; set; }
    }
}
