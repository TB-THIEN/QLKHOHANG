using System;

namespace QuanLyKhoWeb.Models.Entities;

// Cấu hình cửa hàng: bảng chỉ có 1 dòng (MaCauHinh = 1).
// Dùng cho phần "người bán" trên hóa đơn điện tử và phiếu giao hàng.
public partial class CauHinhCuaHang
{
    public int MaCauHinh { get; set; }

    public string TenCuaHang { get; set; } = null!;

    public string? DiaChi { get; set; }

    public string? SoDienThoai { get; set; }

    public string? Email { get; set; }

    public string? NguoiDaiDien { get; set; }

    public string? GhiChuHoaDon { get; set; }

    public string? DuongDanLogo { get; set; }

    public DateTime NgayCapNhat { get; set; }
}
