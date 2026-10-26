namespace QuanLyKhoWeb.Services.Interfaces
{
    // sp_TaoDonHang (bản mới nhất) chỉ nhận ma_sku + so_luong trong ChiTietDonHang_Type;
    // đơn giá được SP tự lấy từ gia_niem_yet tại thời điểm bán, không nhận từ client.
    public class TaoDonHangItem
    {
        public int MaSku { get; set; }
        public int SoLuong { get; set; }
    }

    public interface IDonHangService
    {
        Task<(bool ThanhCong, string ThongBao)> HuyDonHangAsync(int maDonHang, int maNguoiThucHien, string? lyDoHuy);

        // Khớp đúng chữ ký sp_TaoDonHang mới nhất: tên/SĐT/địa chỉ khách được SP lưu
        // trực tiếp vào đơn (ten_khach_hang_giao/sdt_khach_hang_giao/dia_chi_giao_hang);
        // tiền giảm giá do SP tự tính theo mã khuyến mãi, không truyền từ client.
        Task<(bool ThanhCong, string ThongBao, int? MaDonHang)> TaoDonHangAsync(
            int maNhanVien, int? maKhachHang,
            string? tenKhachHang, string? sdtKhachHang, string? diaChiGiaoHang,
            int? maKhuyenMai, decimal phiVanChuyen, string hinhThucNhanHang,
            string? ghiChu, List<TaoDonHangItem> chiTiet);
    }
}