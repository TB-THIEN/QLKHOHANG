using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class BaoCao
{
    public int MaBaoCao { get; set; }

    public string MaCodeBaoCao { get; set; } = null!;

    public string TenBaoCao { get; set; } = null!;

    public string LoaiBaoCao { get; set; } = null!;

    public int? NguoiTao { get; set; }

    public DateTime? NgayTao { get; set; }

    public string? TomTatBoLoc { get; set; }

    public string? DuongDanFile { get; set; }

    public string? NoiDungBaoCaoJson { get; set; }

    public string? GhiChu { get; set; }

    public virtual NguoiDung? NguoiTaoNavigation { get; set; }
}
