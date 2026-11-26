using System.Security.Claims;

namespace QuanLyKhoWeb.Authorization
{
    // Phân quyền theo vai trò: mỗi vai trò có bộ quyền riêng cho từng nhóm trang.
    // Controller, policy và view đều hỏi cùng một chỗ này để không bị lệch nhau.
    public static class QuyenNguoiDung
    {
        public const string QuanLy = "Quản lý";
        public const string NhanVienKho = "Nhân viên kho";
        public const string NhanVienBanHang = "Nhân viên bán hàng";
        public const string NhanVienGiaoHang = "Nhân viên giao hàng";

        public static string TenVaiTro(ClaimsPrincipal? u) => u?.FindFirst("VaiTro")?.Value ?? "";

        public static bool LaQuanLy(ClaimsPrincipal? u) => TenVaiTro(u) == QuanLy;
        public static bool LaNhanVienKho(ClaimsPrincipal? u) => TenVaiTro(u) == NhanVienKho;
        public static bool LaNhanVienBanHang(ClaimsPrincipal? u) => TenVaiTro(u) == NhanVienBanHang;

        // Nhân viên giao hàng: vai trò chuẩn, hoặc vai trò tự tạo có bật cờ giao_hang.
        // Ba vai trò còn lại không bị coi là giao hàng dù có bật cờ này.
        public static bool LaNhanVienGiaoHang(ClaimsPrincipal? u)
        {
            if (LaQuanLy(u) || LaNhanVienKho(u) || LaNhanVienBanHang(u)) return false;
            if (TenVaiTro(u) == NhanVienGiaoHang) return true;
            return u?.FindFirst("GiaoHang")?.Value == "True";
        }

        // Báo cáo: Quản lý + Nhân viên bán hàng (Nhân viên kho và giao hàng không có trang này)
        public static bool XemBaoCao(ClaimsPrincipal? u) => LaQuanLy(u) || LaNhanVienBanHang(u);

        // Danh sách nhân viên: chỉ Quản lý
        public static bool QuanLyNhanVien(ClaimsPrincipal? u) => LaQuanLy(u);

        // Khuyến mãi: Quản lý + Nhân viên bán hàng được thêm/sửa/xóa
        public static bool SuaKhuyenMai(ClaimsPrincipal? u) => LaQuanLy(u) || LaNhanVienBanHang(u);

        // Đơn hàng: Quản lý + Nhân viên bán hàng
        public static bool SuaDonHang(ClaimsPrincipal? u) => LaQuanLy(u) || LaNhanVienBanHang(u);

        // Hủy đơn: theo cột co_quyen_huy_don của vai trò (hiện chỉ Quản lý bật)
        public static bool HuyDon(ClaimsPrincipal? u) => u?.FindFirst("CoQuyenHuyDon")?.Value == "True";

        // Vận chuyển: gán nhân viên giao / xóa vận đơn -> Quản lý + Nhân viên bán hàng
        public static bool SuaVanChuyen(ClaimsPrincipal? u) => LaQuanLy(u) || LaNhanVienBanHang(u);

        // Xác nhận đã giao: Quản lý + Nhân viên bán hàng + Nhân viên giao hàng (chỉ vận đơn của mình)
        public static bool XacNhanGiao(ClaimsPrincipal? u) =>
            LaQuanLy(u) || LaNhanVienBanHang(u) || LaNhanVienGiaoHang(u);

        // Nhà cung cấp: chỉ Quản lý được thêm/sửa/xóa
        public static bool SuaNhaCungCap(ClaimsPrincipal? u) => LaQuanLy(u);

        // Cấu hình cửa hàng (thông tin người bán in trên hóa đơn / phiếu giao hàng): chỉ Quản lý
        public static bool SuaCauHinhCuaHang(ClaimsPrincipal? u) => LaQuanLy(u);

        // In hóa đơn / phiếu giao hàng: mọi vai trò đã đăng nhập đều in được
        // (dữ liệu in ra đúng phạm vi họ được xem, chặn riêng ở controller vận chuyển).
        public static bool InChungTu(ClaimsPrincipal? u)
            => LaQuanLy(u) || LaNhanVienKho(u) || LaNhanVienBanHang(u) || LaNhanVienGiaoHang(u);

        // Sản phẩm / Khu hàng: Quản lý + Nhân viên kho + Nhân viên bán hàng
        public static bool SuaSanPham(ClaimsPrincipal? u) =>
            LaQuanLy(u) || LaNhanVienKho(u) || LaNhanVienBanHang(u);
        public static bool SuaKhuHang(ClaimsPrincipal? u) => SuaSanPham(u);

        // Nhập kho (tạo/hoàn tất/hủy phiếu nhập, quét mã cộng tồn): Quản lý + Nhân viên kho
        public static bool NhapKho(ClaimsPrincipal? u) => LaQuanLy(u) || LaNhanVienKho(u);

        // Mã người dùng đang đăng nhập (null nếu không đọc được claim)
        public static int? MaNguoiDung(ClaimsPrincipal? u) =>
            int.TryParse(u?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
    }
}
