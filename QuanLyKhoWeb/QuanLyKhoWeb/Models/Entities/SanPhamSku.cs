using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class SanPhamSku
{
    public int MaSku { get; set; }

    public string MaCodeSku { get; set; } = null!;

    public int MaMauSanPham { get; set; }

    public int? MaLoHang { get; set; }

    public decimal GiaNiemYet { get; set; }

    public int SoLuong { get; set; }

    public string? TrangThaiVongDoi { get; set; }

    public virtual ICollection<ChiTietDonHang> ChiTietDonHangs { get; set; } = new List<ChiTietDonHang>();

    public virtual ICollection<ChiTietPhieuNhapKho> ChiTietPhieuNhapKhos { get; set; } = new List<ChiTietPhieuNhapKho>();

    public virtual MauSanPham MaMauSanPhamNavigation { get; set; } = null!;

    public virtual LoHang? MaLoHangNavigation { get; set; }

    public virtual ICollection<NhatKyTonKho> NhatKyTonKhos { get; set; } = new List<NhatKyTonKho>();

    public virtual ICollection<PhanBoXuatKhoTheoKe> PhanBoXuatKhoTheoKes { get; set; } = new List<PhanBoXuatKhoTheoKe>();

    public virtual ICollection<TonKhoKeHang> TonKhoKeHangs { get; set; } = new List<TonKhoKeHang>();

    public virtual ICollection<TonKhoKhu> TonKhoKhus { get; set; } = new List<TonKhoKhu>();
}
