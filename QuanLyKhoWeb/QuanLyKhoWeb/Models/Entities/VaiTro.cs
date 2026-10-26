using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class VaiTro
{
    public int MaVaiTro { get; set; }

    public string TenVaiTro { get; set; } = null!;

    public bool CoQuyenHuyDon { get; set; }

    public bool DuocNhanVanDon { get; set; }

    public DateTime NgayTao { get; set; }

    public virtual ICollection<NguoiDung> NguoiDungs { get; set; } = new List<NguoiDung>();
}
