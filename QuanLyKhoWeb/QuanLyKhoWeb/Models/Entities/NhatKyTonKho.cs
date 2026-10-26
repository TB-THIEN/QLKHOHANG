using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class NhatKyTonKho
{
    public long MaNhatKy { get; set; }

    public int MaSku { get; set; }

    public string LoaiBienDong { get; set; } = null!;

    public int SoLuongThayDoi { get; set; }

    public int? MaNguoiThucHien { get; set; }

    public int? MaDonHang { get; set; }

    public int? MaPhieuNhap { get; set; }

    public int? MaKeHang { get; set; }

    public DateTime ThoiGian { get; set; }

    public virtual NguoiDung? MaNguoiThucHienNavigation { get; set; }

    public virtual SanPhamSku MaSkuNavigation { get; set; } = null!;
}
