using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class MauSanPham
{
    public int MaMauSanPham { get; set; }

    public int MaDanhMuc { get; set; }

    public string TenSanPham { get; set; } = null!;

    public string? MaCodeTenSp { get; set; }

    public string? MoTa { get; set; }

    // Ảnh đại diện (bản sao của ảnh có thứ tự nhỏ nhất trong HinhAnhSanPhams)
    public string? DuongDanHinhAnh { get; set; }

    public string? NhanHieu { get; set; }

    public string? LoaiSanPham { get; set; }

    public string? ChatLieu { get; set; }

    public string? MauSac { get; set; }

    public virtual DanhMuc MaDanhMucNavigation { get; set; } = null!;

    public virtual ICollection<HinhAnhSanPham> HinhAnhSanPhams { get; set; } = new List<HinhAnhSanPham>();

    public virtual ICollection<SanPhamSku> SanPhamSkus { get; set; } = new List<SanPhamSku>();
}
