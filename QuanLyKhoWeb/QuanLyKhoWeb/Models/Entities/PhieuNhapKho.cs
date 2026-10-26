using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class PhieuNhapKho
{
    public int MaPhieuNhap { get; set; }

    public string MaCodePhieuNhap { get; set; } = null!;

    public int? MaNhaCungCap { get; set; }

    public int? NguoiTao { get; set; }

    public DateTime? NgayNhap { get; set; }

    public string? TrangThai { get; set; }

    public string? GhiChu { get; set; }

    public DateTime? NgayTao { get; set; }

    public decimal? TongTienHang { get; set; }

    public virtual ICollection<ChiTietPhieuNhapKho> ChiTietPhieuNhapKhos { get; set; } = new List<ChiTietPhieuNhapKho>();

    public virtual NhaCungCap? MaNhaCungCapNavigation { get; set; }

    public virtual NguoiDung? NguoiTaoNavigation { get; set; }
}
