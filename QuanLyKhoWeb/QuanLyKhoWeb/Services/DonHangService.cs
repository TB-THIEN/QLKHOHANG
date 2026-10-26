using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using QuanLyKhoWeb.Services.Interfaces;

namespace QuanLyKhoWeb.Services
{
    public class DonHangService : IDonHangService
    {
        private readonly IDbConnectionFactory _connectionFactory;
        public DonHangService(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

        public async Task<(bool ThanhCong, string ThongBao)> HuyDonHangAsync(int maDonHang, int maNguoiThucHien, string? lyDoHuy)
        {
            using var conn = (SqlConnection)_connectionFactory.CreateConnection();
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@ma_don_hang", maDonHang);
                parameters.Add("@ma_nguoi_thuc_hien", maNguoiThucHien);
                parameters.Add("@ly_do_huy", lyDoHuy);

                await conn.OpenAsync();
                await conn.ExecuteAsync("sp_HuyDonHang", parameters, commandType: CommandType.StoredProcedure);
                return (true, "Hủy đơn hàng thành công.");
            }
            catch (SqlException ex)
            {
                // Bắt các RAISERROR trong SP, vd: "Chỉ có quản lý mới có quyền hủy đơn hàng."
                return (false, ex.Message);
            }
        }

        public async Task<(bool ThanhCong, string ThongBao, int? MaDonHang)> TaoDonHangAsync(
            int maNhanVien, int? maKhachHang,
            string? tenKhachHang, string? sdtKhachHang, string? diaChiGiaoHang,
            int? maKhuyenMai, decimal phiVanChuyen, string hinhThucNhanHang,
            string? ghiChu, List<TaoDonHangItem> chiTiet)
        {
            using var conn = (SqlConnection)_connectionFactory.CreateConnection();
            try
            {
                // ChiTietDonHang_Type (bản mới nhất) chỉ còn 2 cột: ma_sku, so_luong.
                var tbl = new DataTable();
                tbl.Columns.Add("ma_sku", typeof(int));
                tbl.Columns.Add("so_luong", typeof(int));
                foreach (var it in chiTiet)
                    tbl.Rows.Add(it.MaSku, it.SoLuong);

                var p = new DynamicParameters();
                p.Add("@ma_nhan_vien", maNhanVien);
                p.Add("@ma_khach_hang", maKhachHang);
                p.Add("@ten_khach_hang", tenKhachHang);
                p.Add("@sdt_khach_hang", sdtKhachHang);
                p.Add("@dia_chi_giao_hang", diaChiGiaoHang);
                p.Add("@ma_khuyen_mai", maKhuyenMai);
                p.Add("@phi_van_chuyen", phiVanChuyen);
                p.Add("@hinh_thuc_nhan_hang", hinhThucNhanHang);
                p.Add("@ghi_chu", ghiChu);
                p.Add("@chi_tiet", tbl.AsTableValuedParameter("ChiTietDonHang_Type"));

                await conn.OpenAsync();
                // SP tự tính tiền giảm theo mã khuyến mãi + tự lấy đơn giá từ gia_niem_yet,
                // trả về (ma_don_hang, ma_code_don_hang), đồng thời tự tạo vận đơn nếu GIAO_HANG.
                // Đơn tạo ra luôn là đơn thành công (COMPLETED + PAID) vì đã chốt và nhận tiền.
                var row = await conn.QueryFirstOrDefaultAsync<(int ma_don_hang, string ma_code_don_hang)>(
                    "sp_TaoDonHang", p, commandType: CommandType.StoredProcedure);
                return (true, $"Tạo đơn {row.ma_code_don_hang} thành công.", row.ma_don_hang);
            }
            catch (SqlException ex)
            {
                // Bắt các RAISERROR trong SP: SP không hợp lệ, mã KM hết hạn/chưa đạt tối
                // thiểu/hết lượt, danh sách sản phẩm không hợp lệ...
                return (false, ex.Message, null);
            }
        }
    }
}