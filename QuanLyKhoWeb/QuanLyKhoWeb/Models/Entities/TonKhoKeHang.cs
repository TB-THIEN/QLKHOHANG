using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class TonKhoKeHang
{
    public int MaSku { get; set; }

    public int MaKeHang { get; set; }

    public int SoLuong { get; set; }

    public virtual KeHang MaKeHangNavigation { get; set; } = null!;

    public virtual SanPhamSku MaSkuNavigation { get; set; } = null!;
}
