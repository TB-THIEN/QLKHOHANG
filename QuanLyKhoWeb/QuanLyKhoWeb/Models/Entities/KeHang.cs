using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class KeHang
{
    public int MaKeHang { get; set; }

    public int MaKhuVuc { get; set; }

    public string TenKeHang { get; set; } = null!;

    public DateTime NgayTao { get; set; }

    public virtual KhuVucKho MaKhuVucNavigation { get; set; } = null!;

    public virtual ICollection<PhanBoXuatKhoTheoKe> PhanBoXuatKhoTheoKes { get; set; } = new List<PhanBoXuatKhoTheoKe>();

    public virtual ICollection<TonKhoKeHang> TonKhoKeHangs { get; set; } = new List<TonKhoKeHang>();
}
