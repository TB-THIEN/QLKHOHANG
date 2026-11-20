using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class NhaCungCap
{
    public int MaNhaCungCap { get; set; }

    public string MaCodeNcc { get; set; } = null!;

    public string TenNhaCungCap { get; set; } = null!;

    public string? SoDienThoai { get; set; }

    public string? Email { get; set; }

    public string? DiaChi { get; set; }

    public bool? TrangThaiHoatDong { get; set; }

    public DateTime? NgayTao { get; set; }

    public virtual ICollection<PhieuNhapKho> PhieuNhapKhos { get; set; } = new List<PhieuNhapKho>();
}
