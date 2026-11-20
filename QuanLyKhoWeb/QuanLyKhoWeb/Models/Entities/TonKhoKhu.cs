using System;

namespace QuanLyKhoWeb.Models.Entities;

public partial class TonKhoKhu
{
    public int MaSku { get; set; }

    public int MaKhuVuc { get; set; }

    public int SoLuong { get; set; }

    public virtual KhuVucKho MaKhuVucNavigation { get; set; } = null!;

    public virtual SanPhamSku MaSkuNavigation { get; set; } = null!;
}
