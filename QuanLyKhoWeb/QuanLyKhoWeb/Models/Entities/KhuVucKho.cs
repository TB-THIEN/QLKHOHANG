using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class KhuVucKho
{
    public int MaKhuVuc { get; set; }

    public string TenKhuVuc { get; set; } = null!;

    public string LoaiKhuVuc { get; set; } = null!;

    public decimal? DienTichM2 { get; set; }

    public bool? TrangThaiHoatDong { get; set; }

    public virtual ICollection<KeHang> KeHangs { get; set; } = new List<KeHang>();

    public virtual ICollection<TonKhoKhu> TonKhoKhus { get; set; } = new List<TonKhoKhu>();
}
