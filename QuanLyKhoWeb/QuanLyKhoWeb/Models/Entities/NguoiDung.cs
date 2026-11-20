using System;
using System.Collections.Generic;

namespace QuanLyKhoWeb.Models.Entities;

public partial class NguoiDung
{
    public int MaNguoiDung { get; set; }

    public string TenDangNhap { get; set; } = null!;

    public string MatKhauMaHoa { get; set; } = null!;

    public string HoTen { get; set; } = null!;

    public int MaVaiTro { get; set; }

    public bool? TrangThaiHoatDong { get; set; }

    public DateTime? NgayTao { get; set; }

    public DateTime? NgayCapNhat { get; set; }

    public string? Email { get; set; }

    public string? ThuongTru { get; set; }

    public bool PhaiDoiMatKhau { get; set; }

    public string? SoDienThoai { get; set; }

    public DateOnly? NgaySinh { get; set; }

    public string? GioiTinh { get; set; }

    public string? Cccd { get; set; }

    public string? AnhThe { get; set; }

    public string? StkNganHang { get; set; }

    public string? ChiNhanhNganHang { get; set; }

    public virtual ICollection<DonHang> DonHangs { get; set; } = new List<DonHang>();

    public virtual VaiTro MaVaiTroNavigation { get; set; } = null!;

    public virtual ICollection<MatKhauDatLai> MatKhauDatLais { get; set; } = new List<MatKhauDatLai>();

    public virtual ICollection<NhatKyThayDoiNguoiDung> NhatKyThayDoiNguoiDungMaNguoiDungNavigations { get; set; } = new List<NhatKyThayDoiNguoiDung>();

    public virtual ICollection<NhatKyThayDoiNguoiDung> NhatKyThayDoiNguoiDungMaNguoiThucHienNavigations { get; set; } = new List<NhatKyThayDoiNguoiDung>();

    public virtual ICollection<NhatKyTonKho> NhatKyTonKhos { get; set; } = new List<NhatKyTonKho>();

    public virtual ICollection<PhieuNhapKho> PhieuNhapKhos { get; set; } = new List<PhieuNhapKho>();

    public virtual ICollection<VanDon> VanDons { get; set; } = new List<VanDon>();
}
