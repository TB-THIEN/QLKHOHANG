using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class DonHang
{
    public int MaDonHang { get; set; }

    public string MaCodeDonHang { get; set; } = null!;

    public int? MaKhachHang { get; set; }

    public int? MaNhanVien { get; set; }

    public int? MaKhuyenMai { get; set; }

    public DateTime? NgayDatHang { get; set; }

    public string? TrangThai { get; set; }

    public string? TrangThaiThanhToan { get; set; }

    public string HinhThucNhanHang { get; set; } = null!;

    public decimal TongTienHang { get; set; }

    public decimal TienGiamGia { get; set; }

    public decimal PhiVanChuyen { get; set; }

    public decimal ThanhTien { get; set; }

    public string? GhiChu { get; set; }

    public DateTime? NgayHuy { get; set; }

    public string? LyDoHuy { get; set; }

    public DateTime? NgayTao { get; set; }

    public DateTime? NgayCapNhat { get; set; }

    public string? TenKhachHangGiao { get; set; }

    public string? SdtKhachHangGiao { get; set; }

    public string? DiaChiGiaoHang { get; set; }

    public virtual ICollection<ChiTietDonHang> ChiTietDonHangs { get; set; } = new List<ChiTietDonHang>();

    public virtual KhachHang? MaKhachHangNavigation { get; set; }

    public virtual KhuyenMai? MaKhuyenMaiNavigation { get; set; }

    public virtual NguoiDung? MaNhanVienNavigation { get; set; }

    public virtual ICollection<PhanBoXuatKhoTheoKe> PhanBoXuatKhoTheoKes { get; set; } = new List<PhanBoXuatKhoTheoKe>();

    public virtual VanDon? VanDon { get; set; }
}
