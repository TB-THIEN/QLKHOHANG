using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class ChiTietPhieuNhapKho
{
    public int MaChiTietPhieuNhap { get; set; }

    public int MaPhieuNhap { get; set; }

    public int MaSku { get; set; }

    public int SoLuongKeHoach { get; set; }

    public int SoLuongThucNhan { get; set; }

    public decimal? DonGiaNhap { get; set; }

    public decimal? ThanhTien { get; set; }

    public virtual PhieuNhapKho MaPhieuNhapNavigation { get; set; } = null!;

    public virtual SanPhamSku MaSkuNavigation { get; set; } = null!;
}
