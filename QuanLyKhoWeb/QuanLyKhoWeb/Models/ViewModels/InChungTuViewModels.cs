using QuanLyKhoWeb.Models.Entities;

namespace QuanLyKhoWeb.Models.ViewModels
{
    // Dữ liệu cho trang in hóa đơn điện tử (A4, in bằng Ctrl+P / window.print()).
    public class InHoaDonVm
    {
        public DonHang DonHang { get; set; } = null!;
        public CauHinhCuaHang CuaHang { get; set; } = null!;
    }

    // Dữ liệu cho trang in phiếu giao hàng (A4, khổ dọc).
    public class InPhieuGiaoHangVm
    {
        public VanDon VanDon { get; set; } = null!;
        public DonHang DonHang { get; set; } = null!;
        public CauHinhCuaHang CuaHang { get; set; } = null!;
    }
}
