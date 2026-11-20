using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class PhanBoXuatKhoTheoKe
{
    public int MaPhanBo { get; set; }

    public int MaDonHang { get; set; }

    public int MaSku { get; set; }

    public int MaKeHang { get; set; }

    public int SoLuong { get; set; }

    public virtual DonHang MaDonHangNavigation { get; set; } = null!;

    public virtual KeHang MaKeHangNavigation { get; set; } = null!;

    public virtual SanPhamSku MaSkuNavigation { get; set; } = null!;
}
