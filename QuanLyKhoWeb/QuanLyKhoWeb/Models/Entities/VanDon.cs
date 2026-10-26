using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class VanDon
{
    public int MaVanDon { get; set; }

    public string MaCodeVanDon { get; set; } = null!;

    public int MaDonHang { get; set; }

    public int? MaNhanVienGiaoHang { get; set; }

    public string TrangThai { get; set; } = null!;

    public string? GhiChu { get; set; }

    public DateTime NgayTao { get; set; }

    public DateTime NgayCapNhat { get; set; }

    public virtual DonHang MaDonHangNavigation { get; set; } = null!;

    public virtual NguoiDung? MaNhanVienGiaoHangNavigation { get; set; }
}
