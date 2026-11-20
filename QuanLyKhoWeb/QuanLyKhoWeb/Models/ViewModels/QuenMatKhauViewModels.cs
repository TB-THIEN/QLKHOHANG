using System.ComponentModel.DataAnnotations;

namespace QuanLyKhoWeb.Models.ViewModels
{
    // Một trang duy nhất: nhập email -> bấm Gửi mã -> nhập OTP + mật khẩu mới
    public class KhoiPhucMatKhauVm
    {
        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email chưa đúng định dạng")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        public int MaDatLai { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã xác nhận")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác nhận gồm 6 chữ số")]
        [Display(Name = "Mã xác nhận (6 số)")]
        public string MaXacNhan { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
        [Display(Name = "Mật khẩu mới")]
        public string MatKhauMoi { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu")]
        [DataType(DataType.Password)]
        [Compare("MatKhauMoi", ErrorMessage = "Mật khẩu nhập lại không khớp")]
        [Display(Name = "Nhập lại mật khẩu mới")]
        public string XacNhanMatKhau { get; set; } = string.Empty;
    }
}
