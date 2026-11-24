using System;

namespace QuanLyKhoWeb.Models.Entities;

/// <summary>
/// Một ảnh của sản phẩm (mẫu sản phẩm). Thứ tự nhỏ nhất là ảnh đại diện,
/// đồng thời được chép sang mau_san_pham.duong_dan_hinh_anh để các màn hình cũ dùng lại.
/// </summary>
public partial class HinhAnhSanPham
{
    public int MaHinhAnh { get; set; }

    public int MaMauSanPham { get; set; }

    public string DuongDan { get; set; } = null!;

    public int ThuTu { get; set; }

    public virtual MauSanPham MaMauSanPhamNavigation { get; set; } = null!;
}
