using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class NhatKyThayDoiNguoiDung
{
    public int MaNhatKy { get; set; }

    public int MaNguoiDung { get; set; }

    public int? MaNguoiThucHien { get; set; }

    public string TenTruong { get; set; } = null!;

    public string? GiaTriCu { get; set; }

    public string? GiaTriMoi { get; set; }

    public DateTime ThoiGian { get; set; }

    public virtual NguoiDung MaNguoiDungNavigation { get; set; } = null!;

    public virtual NguoiDung? MaNguoiThucHienNavigation { get; set; }
}
