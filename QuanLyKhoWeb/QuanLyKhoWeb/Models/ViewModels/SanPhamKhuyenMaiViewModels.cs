using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace QuanLyKhoWeb.Models.ViewModels
{
    public class SanPhamCreateVm
    {
        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
        [Display(Name = "Tên sản phẩm")]
        public string TenSanPham { get; set; } = string.Empty;

        // Trang Thêm sản phẩm: bắt buộc nhập/chọn (trừ Ảnh, Mô tả, Khu/Kệ vẫn tùy chọn)
        [Required(ErrorMessage = "Vui lòng nhập/chọn danh mục")]
        [Display(Name = "Danh mục")]
        public string? DanhMuc { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập/chọn nhãn hiệu")]
        [Display(Name = "Nhãn hiệu")]
        public string? NhanHieu { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập/chọn loại sản phẩm")]
        [Display(Name = "Loại sản phẩm")]
        public string? LoaiSanPham { get; set; }

        [Display(Name = "Chất liệu")]
        [Required(ErrorMessage = "Vui lòng nhập chất liệu (dùng để sinh mã SKU)")]
        public string? ChatLieu { get; set; }

        [Display(Name = "Màu sắc")]
        [Required(ErrorMessage = "Vui lòng nhập màu sắc (dùng để sinh mã SKU)")]
        public string? MauSac { get; set; }

        // Ngày lô hàng (ngày/tháng/năm): mã lô tự sinh L+yyMMdd,
        // tháng/năm của ngày này dùng để sinh đoạn YYMM trong mã SKU.
        [Required(ErrorMessage = "Vui lòng chọn ngày lô hàng")]
        [Display(Name = "Ngày lô hàng")]
        [DataType(DataType.Date)]
        public DateTime? NgayLoHang { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá tiền")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá phải >= 0")]
        [Display(Name = "Giá tiền")]
        public decimal? GiaTien { get; set; }

        // Vị trí xếp ban đầu (không bắt buộc). Chọn khu mà không chọn kệ = xếp trực tiếp vào khu.
        [Display(Name = "Khu hàng")]
        public int? MaKhuVuc { get; set; }

        [Display(Name = "Kệ hàng")]
        public int? MaKeHang { get; set; }

        [Display(Name = "Số lượng trên kệ")]
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng trên kệ phải >= 0")]
        public int? SoLuongTrenKe { get; set; }

        [Display(Name = "Mô tả")]
        public string? MoTa { get; set; }

        // Chọn nhiều file ảnh trực tiếp từ máy. Ảnh đầu tiên là ảnh đại diện.
        // Controller sẽ lưu file vào wwwroot/images/products rồi mới ghi đường dẫn vào DB.
        [Display(Name = "Hình ảnh")]
        public List<IFormFile>? HinhAnhs { get; set; }

        // Thứ tự ảnh người dùng sắp trên giao diện, dạng "moi:0|moi:1" (JS điền vào).
        public string? ThuTuAnh { get; set; }
    }

    public class SanPhamEditVm
    {
        public int MaSku { get; set; }

        // Mã SKU chốt từ lúc tạo, sửa thông tin không đổi mã
        public string MaCodeSku { get; set; } = string.Empty;

        // Lô hàng chốt từ lúc tạo (chỉ hiển thị)
        public string? LoHang { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
        [Display(Name = "Tên sản phẩm")]
        public string TenSanPham { get; set; } = string.Empty;

        [Display(Name = "Danh mục")]
        public string? DanhMuc { get; set; }

        [Display(Name = "Nhãn hiệu")]
        public string? NhanHieu { get; set; }

        [Display(Name = "Loại sản phẩm")]
        public string? LoaiSanPham { get; set; }

        [Display(Name = "Chất liệu")]
        public string? ChatLieu { get; set; }

        [Display(Name = "Màu sắc")]
        public string? MauSac { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá phải >= 0")]
        [Display(Name = "Giá tiền")]
        public decimal GiaTien { get; set; } = 0;

        [Range(0, int.MaxValue, ErrorMessage = "Số lượng phải >= 0")]
        [Display(Name = "Số lượng")]
        public int SoLuong { get; set; } = 0;

        [Display(Name = "Mô tả")]
        public string? MoTa { get; set; }

        // Danh sách ảnh hiện có (thứ tự hiển thị = thứ tự trong danh sách, ảnh đầu là đại diện).
        // View gửi lên chuỗi đường dẫn còn giữ, ngăn cách bởi dấu "|".
        public string? HinhAnhHienTai { get; set; }

        // Ảnh mới thêm trong lần sửa này (nối vào sau danh sách ảnh còn giữ)
        [Display(Name = "Thêm hình ảnh")]
        public List<IFormFile>? HinhAnhMois { get; set; }

        // Thứ tự cuối cùng: "moi:0|/images/products/a.jpg|moi:1" (JS điền vào).
        public string? ThuTuAnh { get; set; }
    }

    public class KhuyenMaiCreateVm
    {
        [Required(ErrorMessage = "Vui lòng nhập mã khuyến mãi")]
        [Display(Name = "Mã khuyến mãi")]
        public string MaCodeKhuyenMai { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên")]
        [Display(Name = "Tên mã")]
        public string TenKhuyenMai { get; set; } = string.Empty;

        // Chỉ giữ phương án giảm giá đơn hàng
        [Display(Name = "Loại giảm giá")]
        public string LoaiGiamGia { get; set; } = "PERCENT"; // PERCENT | FIXED_AMOUNT

        // Trang Thêm mã: bắt buộc nhập hết (đơn tối thiểu nhập 0 nếu không điều kiện)
        [Required(ErrorMessage = "Vui lòng nhập giá trị giảm")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá trị giảm phải >= 0")]
        [Display(Name = "Giá trị giảm (% hoặc đ)")]
        public decimal? GiaTriGiam { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá trị đơn tối thiểu (nhập 0 nếu không điều kiện)")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá trị đơn tối thiểu phải >= 0")]
        [Display(Name = "Điều kiện: giá trị đơn tối thiểu")]
        public decimal? GiaTriDonToiThieu { get; set; }

        // Chỉ nhập số lần
        [Required(ErrorMessage = "Vui lòng nhập giới hạn sử dụng")]
        [Range(1, int.MaxValue, ErrorMessage = "Số lần phải > 0")]
        [Display(Name = "Giới hạn sử dụng (số lần)")]
        public int? SoLanSuDungToiDa { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu")]
        [Display(Name = "Ngày bắt đầu")]
        public DateTime NgayBatDau { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc (bắt buộc)")]
        [Display(Name = "Ngày kết thúc")]
        public DateTime NgayKetThuc { get; set; } = DateTime.Today.AddMonths(1);
    }
}