namespace QuanLyKhoWeb.Models.ViewModels
{
    /// <summary>Dữ liệu một tháng của biểu đồ doanh thu.</summary>
    public class ThangDoanhThuVm
    {
        public int Thang { get; set; }
        public int SoDon { get; set; }

        /// <summary>Tổng số hàng (cộng số lượng trên các dòng của đơn hoàn tất) trong tháng.</summary>
        public int TongSoHang { get; set; }

        public decimal DoanhThu { get; set; }
    }

    /// <summary>Dữ liệu một tháng của biểu đồ đơn hàng.</summary>
    public class ThangDonHangVm
    {
        public int Thang { get; set; }
        public int TongDon { get; set; }
        public int HoanTat { get; set; }
        public int DaHuy { get; set; }
        public int ChoXuLy { get; set; }
        public decimal GiaTri { get; set; }
    }

    /// <summary>Dòng trong bảng xếp hạng sản phẩm bán chạy.</summary>
    public class TopSanPhamVm
    {
        public int MaSku { get; set; }
        public string MaCodeSku { get; set; } = "";
        public string TenSanPham { get; set; } = "";
        public string? HinhAnh { get; set; }
        public int SoLuong { get; set; }
        public decimal DoanhThu { get; set; }
    }

    /// <summary>Một đơn hàng chi tiết dùng cho bảng kê của tháng.</summary>
    public class DonHangChiTietVm
    {
        public int MaDonHang { get; set; }
        public string MaCodeDonHang { get; set; } = "";
        public DateTime NgayDat { get; set; }
        public string KhachHang { get; set; } = "";
        public string DienThoai { get; set; } = "";
        public string NhanVien { get; set; } = "";
        public string HinhThuc { get; set; } = "";
        public int SoDong { get; set; }
        public int SoLuong { get; set; }
        public decimal TongHang { get; set; }
        public decimal GiamGia { get; set; }
        public decimal PhiVanChuyen { get; set; }
        public decimal ThanhTien { get; set; }
        public string TrangThai { get; set; } = "";
        public string ThanhToan { get; set; } = "";
        public string SanPham { get; set; } = "";
    }

    /// <summary>Một phiếu nhập chi tiết dùng cho bảng nhập kho của tháng.</summary>
    public class PhieuNhapChiTietVm
    {
        public int MaPhieuNhap { get; set; }
        public string MaCodePhieuNhap { get; set; } = "";
        public DateTime NgayNhap { get; set; }
        public string NhaCungCap { get; set; } = "";
        public string NguoiTao { get; set; } = "";
        public int SoDong { get; set; }
        public int SoLuong { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThai { get; set; } = "";
    }

    /// <summary>Toàn bộ dữ liệu của trang Báo cáo, dùng chung cho giao diện và file Excel.</summary>
    public class BaoCaoVm
    {
        public string CheDo { get; set; } = "doanhthu";
        public int Nam { get; set; }
        public int? Thang { get; set; }

        /// <summary>Quý đang xuất (1-4). Chỉ dùng cho file Excel xuất theo quý.</summary>
        public int? Quy { get; set; }

        /// <summary>Các năm có dữ liệu bán hàng/nhập kho (mới nhất trước).</summary>
        public List<int> NamCoDuLieu { get; set; } = new();

        public List<int> DanhSachNam { get; set; } = new();

        public List<ThangDoanhThuVm> DoanhThuThang { get; set; } = new();
        public List<ThangDonHangVm> DonHangThang { get; set; } = new();
        public List<TopSanPhamVm> TopSanPham { get; set; } = new();

        public decimal DoanhThuCaNam { get; set; }
        public int SoDonCaNam { get; set; }
        public int TongSoHangCaNam { get; set; }

        public List<DonHangChiTietVm> DonHangChiTiet { get; set; } = new();
        public List<PhieuNhapChiTietVm> PhieuNhapChiTiet { get; set; } = new();
    }
}
