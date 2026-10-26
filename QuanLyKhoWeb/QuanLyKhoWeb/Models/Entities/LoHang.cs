using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class LoHang
{
    public int MaLoHang { get; set; }

    public string MaCodeLoHang { get; set; } = null!;

    public DateOnly? NgayLoHang { get; set; }

    public string? GhiChu { get; set; }

    public DateTime? NgayTao { get; set; }

    public virtual ICollection<SanPhamSku> SanPhamSkus { get; set; } = new List<SanPhamSku>();
}
