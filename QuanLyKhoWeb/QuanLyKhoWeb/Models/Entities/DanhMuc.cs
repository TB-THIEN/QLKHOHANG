using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class DanhMuc
{
    public int MaDanhMuc { get; set; }

    public string MaCodeDanhMuc { get; set; } = null!;

    public string TenDanhMuc { get; set; } = null!;

    public virtual ICollection<MauSanPham> MauSanPhams { get; set; } = new List<MauSanPham>();
}
