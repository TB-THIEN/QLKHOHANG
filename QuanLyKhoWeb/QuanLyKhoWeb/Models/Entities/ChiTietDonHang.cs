using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class ChiTietDonHang
{
    public int MaChiTietDon { get; set; }

    public int MaDonHang { get; set; }

    public int MaSku { get; set; }

    public int SoLuong { get; set; }

    public decimal DonGia { get; set; }

    public decimal ThanhTienDong { get; set; }

    public virtual DonHang MaDonHangNavigation { get; set; } = null!;

    public virtual SanPhamSku MaSkuNavigation { get; set; } = null!;
}
