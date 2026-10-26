using Microsoft.AspNetCore.Mvc;
using QuanLyKhoWeb.Services.Interfaces;
using System.Data;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;

namespace QuanLyKhoWeb.Controllers
{
    public class BaoCaoController : Controller
    {
        private readonly IDbConnectionFactory _factory;
        public BaoCaoController(IDbConnectionFactory factory) => _factory = factory;

        // Tổng quan: chỉ lọc ngày, bỏ phân tích/so sánh/chỉnh sửa/fullscreen
        public async Task<IActionResult> Index(DateTime? tuNgay, DateTime? denNgay, string nhomTheo = "NGAY", int page = 1)
        {
            ViewData["SearchValue"] = null;
            denNgay ??= DateTime.Today;
            tuNgay ??= DateTime.Today.AddDays(-29);
            if (!new[] { "NGAY", "THANG", "NAM" }.Contains(nhomTheo)) nhomTheo = "NGAY";
            ViewBag.TuNgay = tuNgay.Value.ToString("yyyy-MM-dd");
            ViewBag.DenNgay = denNgay.Value.ToString("yyyy-MM-dd");
            ViewBag.NhomTheo = nhomTheo;

            using var conn = (SqlConnection)_factory.CreateConnection();
            var p = new DynamicParameters();
            p.Add("@tu_ngay", tuNgay.Value.Date);
            p.Add("@den_ngay", denNgay.Value.Date);
            p.Add("@nhom_theo", nhomTheo);
            await conn.OpenAsync();
            var rows = (await conn.QueryAsync<dynamic>("sp_BaoCaoDoanhThuTheoThoiGian", p, commandType: CommandType.StoredProcedure)).ToList();

            // Phân trang 30 dòng = 30 ngày
            const int pageSize = 30;
            var total = rows.Count;
            var items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            ViewBag.Page = page;
            ViewBag.TotalPage = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.TongDon = rows.Sum(r => (int)(r.so_luong_don ?? 0));
            ViewBag.TongDoanhThu = rows.Sum(r => (decimal)(r.doanh_thu ?? 0));

            return View(items);
        }

        // Top bán chạy: Tất cả / Ngày / Tháng / Năm / Tùy chỉnh + click mũi tên sang trang này
        public async Task<IActionResult> TopSanPham(string boLoc = "TAT_CA", DateTime? tuNgay = null, DateTime? denNgay = null)
        {
            ViewBag.BoLoc = boLoc;
            ViewBag.TuNgay = tuNgay?.ToString("yyyy-MM-dd");
            ViewBag.DenNgay = denNgay?.ToString("yyyy-MM-dd");
            using var conn = (SqlConnection)_factory.CreateConnection();
            var p = new DynamicParameters();
            p.Add("@bo_loc", boLoc);
            p.Add("@tu_ngay", tuNgay?.Date);
            p.Add("@den_ngay", denNgay?.Date);
            p.Add("@top_n", 30);
            await conn.OpenAsync();
            var rows = (await conn.QueryAsync<dynamic>("sp_TopSanPhamBanChay", p, commandType: CommandType.StoredProcedure)).ToList();
            return View(rows);
        }

        // Xuất báo cáo: popup xác nhận -> tải CSV (mở bằng Excel) theo thời gian lọc
        public async Task<IActionResult> XuatExcel(DateTime tuNgay, DateTime denNgay, string nhomTheo = "NGAY")
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            var p = new DynamicParameters();
            p.Add("@tu_ngay", tuNgay.Date);
            p.Add("@den_ngay", denNgay.Date);
            p.Add("@nhom_theo", nhomTheo);
            await conn.OpenAsync();
            var rows = (await conn.QueryAsync<dynamic>("sp_BaoCaoDoanhThuTheoThoiGian", p, commandType: CommandType.StoredProcedure)).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("Moc thoi gian,So don,Doanh thu");
            foreach (var r in rows)
            {
                string moc = nhomTheo switch
                {
                    "THANG" => $"{r.thang}/{r.nam}",
                    "NAM" => $"{r.nam}",
                    _ => ((DateTime)r.moc_thoi_gian).ToString("dd/MM/yyyy")
                };
                sb.AppendLine($"{moc},{r.so_luong_don},{r.doanh_thu}");
            }
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"BaoCaoDoanhThu_{tuNgay:yyyyMMdd}_{denNgay:yyyyMMdd}.csv");
        }
    }
}
