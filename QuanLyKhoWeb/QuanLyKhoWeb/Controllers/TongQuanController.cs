using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;

namespace QuanLyKhoWeb.Controllers
{
    public class TongQuanController : Controller
    {
        private readonly DoAnQlkContext _context;
        public TongQuanController(DoAnQlkContext context) => _context = context;

        // Tổng quan: thẻ thống kê + xem đơn hàng từng ngày bằng ô chọn lịch
        // (mặc định hôm nay; chọn ngày khác chỉ hiện đơn ngày đó)
        public async Task<IActionResult> Index(string? search, DateOnly? ngay)
        {
            ViewData["SearchValue"] = search;
            var ngayXem = ngay ?? DateOnly.FromDateTime(DateTime.Today);
            ViewBag.NgayXem = ngayXem.ToString("yyyy-MM-dd");
            ViewBag.NgayXemHienThi = ngayXem.ToString("dd/MM/yyyy");
            var dauNgay = ngayXem.ToDateTime(TimeOnly.MinValue);
            var cuoiNgay = dauNgay.AddDays(1);

            var donTrongNgay = await _context.DonHangs
                .Where(d => d.NgayDatHang >= dauNgay && d.NgayDatHang < cuoiNgay
                    && d.TrangThai != "CANCELLED")
                .ToListAsync();

            // Ô tìm kiếm của trang chỉ lọc bảng đơn bên dưới, số liệu tổng quan giữ nguyên cả ngày.
            var donHienThi = donTrongNgay;
            if (!string.IsNullOrWhiteSpace(search))
                donHienThi = donTrongNgay.Where(d => (d.MaCodeDonHang ?? "").Contains(search)
                    || (d.TenKhachHangGiao ?? "").Contains(search)
                    || (d.SdtKhachHangGiao ?? "").Contains(search)).ToList();

            ViewBag.DonHomNay = donTrongNgay.Count;
            // Doanh thu chỉ tính đơn đã hoàn tất (đơn Chờ xác nhận chưa thu tiền)
            ViewBag.DoanhThuHomNay = donTrongNgay.Where(d => d.TrangThai == "COMPLETED").Sum(d => d.ThanhTien);
            ViewBag.TongTonKho = await _context.SanPhamSkus.SumAsync(s => (int?)s.SoLuong) ?? 0;
            ViewBag.SanPhamSapHet = await _context.SanPhamSkus.CountAsync(s => s.SoLuong <= 5);
            ViewBag.DonMoiNhat = donHienThi.OrderByDescending(d => d.NgayDatHang).Take(30).ToList();

            return View();
        }
    }
}
