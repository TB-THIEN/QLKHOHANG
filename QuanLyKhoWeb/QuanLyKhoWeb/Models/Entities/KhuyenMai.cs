using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class KhuyenMai
{
    public int MaKhuyenMai { get; set; }

    public string MaCodeKhuyenMai { get; set; } = null!;

    public string TenKhuyenMai { get; set; } = null!;

    public string? LoaiGiamGia { get; set; }

    public decimal GiaTriGiam { get; set; }

    public decimal? GiaTriDonToiThieu { get; set; }

    public int? SoLanSuDungToiDa { get; set; }

    public DateTime NgayBatDau { get; set; }

    public DateTime NgayKetThuc { get; set; }

    public bool? TrangThaiHoatDong { get; set; }

    public DateTime? NgayTao { get; set; }

    public int SoLanDaDung { get; set; }

    public virtual ICollection<DonHang> DonHangs { get; set; } = new List<DonHang>();
}
