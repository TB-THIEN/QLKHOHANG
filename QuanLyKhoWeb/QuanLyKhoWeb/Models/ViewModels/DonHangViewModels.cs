using System.ComponentModel.DataAnnotations;

namespace QuanLyKhoWeb.Models.ViewModels
{
    public class DonHangItemVm
    {
        public int MaSku { get; set; }
        public string MaCodeSku { get; set; } = string.Empty;
        public string TenSanPham { get; set; } = string.Empty;
        public decimal DonGia { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải >= 1")]
        public int SoLuong { get; set; } = 1;
    }

    public class DonHangCreateVm
    {
        // Mã đơn tự sinh - hiển thị readonly, không nhập
        public string MaDonHienThi => "(Tự động sinh)";

        [Display(Name = "Mã giảm giá")]
        public int? MaKhuyenMai { get; set; }

        [Display(Name = "Hình thức nhận hàng")]
        public string HinhThucNhanHang { get; set; } = "GIAO_HANG";

        public decimal PhiVanChuyen { get; set; } = 0;

        [Required(ErrorMessage = "Vui lòng nhập tên khách hàng")]
        [Display(Name = "Tên khách hàng")]
        public string? TenKhachHangGiao { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Display(Name = "Số điện thoại")]
        public string? SdtKhachHangGiao { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập địa chỉ")]
        [Display(Name = "Địa chỉ")]
        public string? DiaChiGiaoHang { get; set; }

        public string? GhiChu { get; set; }

        public List<DonHangItemVm> Items { get; set; } = new();
    }
}
