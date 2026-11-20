using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;
using Dapper;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Models.ViewModels;
using QuanLyKhoWeb.Services.Interfaces;

namespace QuanLyKhoWeb.Controllers
{
    // Báo cáo doanh thu/đơn hàng/top bán chạy: Quản lý + Nhân viên bán hàng.
    // Nhân viên kho và Nhân viên giao hàng không có trang này.
    [Authorize(Policy = "XemBaoCao")]
    public class BaoCaoController : Controller
    {
        private readonly IDbConnectionFactory _factory;
        private readonly DoAnQlkContext _context;

        public BaoCaoController(IDbConnectionFactory factory, DoAnQlkContext context)
        {
            _factory = factory;
            _context = context;
        }

        // =====================================================================
        //  TRANG BÁO CÁO: 3 chế độ (Doanh thu / Đơn hàng / Top bán chạy)
        //  Mỗi chế độ: biểu đồ 12 tháng (bấm cột để xem riêng tháng đó) + danh sách 20 dòng/trang
        // =====================================================================
        public async Task<IActionResult> Index(int? nam, string xem = "doanhthu", int? thang = null, int page = 1, string? search = null)
        {
            if (!new[] { "doanhthu", "donhang", "top" }.Contains(xem)) xem = "doanhthu";
            if (page < 1) page = 1;

            var context = await XayDungBaoCaoAsync(xem, nam, thang);

            // Ô tìm kiếm của trang: lọc các bảng chi tiết theo từ khóa người dùng gõ.
            ViewData["SearchValue"] = search;
            var kpiTopSoSP = context.TopSanPham.Count;
            var kpiTopSoLuong = context.TopSanPham.Sum(p => p.SoLuong);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var tuKhoa = search.Trim();
                bool Khop(params string?[] giaTri)
                    => giaTri.Any(g => g != null && g.Contains(tuKhoa, StringComparison.OrdinalIgnoreCase));

                context.DonHangChiTiet = context.DonHangChiTiet
                    .Where(d => Khop(d.MaCodeDonHang, d.KhachHang, d.DienThoai, d.NhanVien, d.SanPham, d.TrangThai, d.ThanhToan))
                    .ToList();
                context.PhieuNhapChiTiet = context.PhieuNhapChiTiet
                    .Where(p => Khop(p.MaCodePhieuNhap, p.NhaCungCap, p.NguoiTao, p.TrangThai))
                    .ToList();
                context.TopSanPham = context.TopSanPham
                    .Where(p => Khop(p.TenSanPham, p.MaCodeSku))
                    .ToList();
            }

            // KPI của 3 chế độ dùng cho các nút chọn chế độ (tính trên toàn năm, không theo từ khóa)
            ViewBag.KpiDoanhThu = context.DoanhThuCaNam;
            ViewBag.KpiSoDon = context.SoDonCaNam;
            ViewBag.KpiTopSoSP = kpiTopSoSP;
            ViewBag.KpiTopSoLuong = kpiTopSoLuong;

            ViewBag.CheDo = xem;
            ViewBag.Nam = context.Nam;
            ViewBag.ThangDangChon = context.Thang;
            ViewBag.DanhSachNam = context.DanhSachNam;
            ViewBag.Page = page;

            return View(context);
        }

        // =====================================================================
        //  XUẤT BÁO CÁO EXCEL (.xlsx) — MỘT file gộp:
        //  - Sheet 1: tổng hợp 12 tháng (số đơn, tổng số hàng, doanh thu, tỷ trọng...)
        //  - Sheet 2: chi tiết tháng đang chọn (nếu người dùng đang lọc theo tháng)
        // =====================================================================
        public async Task<IActionResult> XuatExcel(string xem = "doanhthu", int? nam = null, int? thang = null)
        {
            if (!new[] { "doanhthu", "donhang", "top" }.Contains(xem)) xem = "doanhthu";
            var context = await XayDungBaoCaoAsync(xem, nam, thang, layChiTiet: true);

            var bytes = BaoCaoExcelBuilder.XayDung(context);
            var tenFile = BaoCaoExcelBuilder.TenFile(context);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", tenFile);
        }

        // Trang top bán chạy riêng vẫn giữ để link cũ không gãy.
        public IActionResult TopSanPham() => RedirectToAction(nameof(Index), new { xem = "top" });

        // =====================================================================
        //  Dữ liệu dùng chung cho cả trang và file Excel
        // =====================================================================
        private async Task<BaoCaoVm> XayDungBaoCaoAsync(string xem, int? nam, int? thang, bool layChiTiet = false)
        {
            var namChon = nam ?? DateTime.Today.Year;
            if (namChon < 2000 || namChon > 2100) namChon = DateTime.Today.Year;

            int? thangChon = thang;
            if (thangChon is < 1 or > 12) thangChon = null;

            var vm = new BaoCaoVm { CheDo = xem, Nam = namChon, Thang = thangChon };

            // Các năm có dữ liệu (đơn hàng hoặc phiếu nhập), luôn kèm năm hiện tại và năm đang chọn.
            var dsNam = new SortedSet<int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
            dsNam.Add(DateTime.Today.Year);
            dsNam.Add(namChon);
            using (var conn = (SqlConnection)_factory.CreateConnection())
            {
                await conn.OpenAsync();
                var cacNam = await conn.QueryAsync<int>(
                    "SELECT DISTINCT YEAR(ngay_dat_hang) FROM don_hang WHERE ngay_dat_hang IS NOT NULL UNION SELECT DISTINCT YEAR(ngay_nhap) FROM phieu_nhap_kho WHERE ngay_nhap IS NOT NULL");
                foreach (var y in cacNam) if (y >= 2000 && y <= 2100) dsNam.Add(y);
            }
            vm.DanhSachNam = dsNam.ToList();

            // ---- Doanh thu 12 tháng ----
            var doanhThu = await TinhDoanhThu12ThangAsync(namChon);
            vm.DoanhThuThang = doanhThu;
            vm.DoanhThuCaNam = doanhThu.Sum(x => x.DoanhThu);
            vm.SoDonCaNam = doanhThu.Sum(x => x.SoDon);
            vm.TongSoHangCaNam = doanhThu.Sum(x => x.TongSoHang);

            // ---- Đơn hàng 12 tháng ----
            vm.DonHangThang = await TinhDonHang12ThangAsync(namChon);

            // ---- Top sản phẩm trong năm ----
            vm.TopSanPham = await LayTopSanPhamAsync(namChon);

            if (thangChon is int t)
            {
                vm.DonHangChiTiet = await LayDonHangTrongThangAsync(namChon, t);
                vm.PhieuNhapChiTiet = await LayPhieuNhapTrongThangAsync(namChon, t);
            }
            else if (layChiTiet && xem == "top")
            {
                // Top bán chạy xuất theo năm: lấy toàn bộ dòng bán trong năm
                vm.DonHangChiTiet = await LayDonHangTrongThangAsync(namChon, null);
            }

            return vm;
        }

        // ---------------------------------------------------------------- doanh thu
        private async Task<List<ThangDoanhThuVm>> TinhDoanhThu12ThangAsync(int namChon)
        {
            var tuNgay = new DateTime(namChon, 1, 1);
            var denNgay = new DateTime(namChon, 12, 31);

            using var conn = (SqlConnection)_factory.CreateConnection();
            var p = new DynamicParameters();
            p.Add("@tu_ngay", tuNgay);
            p.Add("@den_ngay", denNgay);
            p.Add("@nhom_theo", "THANG");
            await conn.OpenAsync();
            var rows = (await conn.QueryAsync<dynamic>("sp_BaoCaoDoanhThuTheoThoiGian", p, commandType: CommandType.StoredProcedure)).ToList();

            var map = new Dictionary<int, (int don, decimal tien)>();
            foreach (var r in rows)
            {
                var thang = (int)r.thang;
                map[thang] = ((int)r.so_luong_don, (decimal)r.doanh_thu);
            }

            // Tổng số hàng bán ra trong tháng: cộng số lượng các dòng của đơn hoàn tất
            // (cùng phạm vi với doanh thu để hai cột luôn khớp nhau).
            var denNgayKeTiep = denNgay.AddDays(1);
            var soLuongTheoThang = await _context.ChiTietDonHangs
                .AsNoTracking()
                .Where(c => c.MaDonHangNavigation.TrangThai == "COMPLETED"
                    && c.MaDonHangNavigation.NgayDatHang >= tuNgay
                    && c.MaDonHangNavigation.NgayDatHang < denNgayKeTiep)
                .GroupBy(c => c.MaDonHangNavigation.NgayDatHang!.Value.Month)
                .Select(g => new { Thang = g.Key, SoLuong = g.Sum(x => x.SoLuong) })
                .ToListAsync();

            var mapSoLuong = soLuongTheoThang.ToDictionary(x => x.Thang, x => x.SoLuong);

            var list = new List<ThangDoanhThuVm>();
            for (int m = 1; m <= 12; m++)
            {
                var has = map.TryGetValue(m, out var v);
                list.Add(new ThangDoanhThuVm
                {
                    Thang = m,
                    SoDon = has ? v.don : 0,
                    TongSoHang = mapSoLuong.TryGetValue(m, out var sl) ? sl : 0,
                    DoanhThu = has ? v.tien : 0m
                });
            }
            return list;
        }

        // ---------------------------------------------------------------- đơn hàng
        private async Task<List<ThangDonHangVm>> TinhDonHang12ThangAsync(int namChon)
        {
            var tuNgay = new DateTime(namChon, 1, 1);
            var denNgay = new DateTime(namChon, 12, 31).AddDays(1);

            var rows = await _context.DonHangs
                .AsNoTracking()
                .Where(d => d.NgayDatHang >= tuNgay && d.NgayDatHang < denNgay)
                .GroupBy(d => d.NgayDatHang!.Value.Month)
                .Select(g => new
                {
                    Thang = g.Key,
                    TongDon = g.Count(),
                    HoanTat = g.Count(d => d.TrangThai == "COMPLETED"),
                    DaHuy = g.Count(d => d.TrangThai == "CANCELLED"),
                    ChoXuLy = g.Count(d => d.TrangThai != "COMPLETED" && d.TrangThai != "CANCELLED"),
                    GiaTri = g.Sum(d => d.ThanhTien)
                })
                .ToListAsync();

            var map = rows.ToDictionary(r => r.Thang);
            var list = new List<ThangDonHangVm>();
            for (int m = 1; m <= 12; m++)
            {
                if (map.TryGetValue(m, out var v))
                {
                    list.Add(new ThangDonHangVm
                    {
                        Thang = m, TongDon = v.TongDon, HoanTat = v.HoanTat,
                        DaHuy = v.DaHuy, ChoXuLy = v.ChoXuLy, GiaTri = v.GiaTri
                    });
                }
                else
                {
                    list.Add(new ThangDonHangVm { Thang = m });
                }
            }
            return list;
        }

        // ---------------------------------------------------------------- top sản phẩm
        private async Task<List<TopSanPhamVm>> LayTopSanPhamAsync(int namChon)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            var p = new DynamicParameters();
            p.Add("@bo_loc", "TUY_CHON");
            p.Add("@tu_ngay", new DateTime(namChon, 1, 1));
            p.Add("@den_ngay", new DateTime(namChon, 12, 31));
            p.Add("@top_n", 200);
            await conn.OpenAsync();
            var rows = (await conn.QueryAsync<dynamic>("sp_TopSanPhamBanChay", p, commandType: CommandType.StoredProcedure)).ToList();

            return rows.Select(r => new TopSanPhamVm
            {
                MaSku = (int)r.ma_sku,
                MaCodeSku = (string)r.ma_code_sku,
                TenSanPham = (string)r.ten_san_pham,
                HinhAnh = (string?)r.duong_dan_hinh_anh,
                SoLuong = (int)r.tong_so_luong_ban,
                DoanhThu = (decimal)r.tong_doanh_thu
            }).ToList();
        }

        // ---------------------------------------------------------------- chi tiết tháng
        private async Task<List<DonHangChiTietVm>> LayDonHangTrongThangAsync(int namChon, int? thang)
        {
            var tu = new DateTime(namChon, thang ?? 1, 1);
            var den = thang.HasValue ? tu.AddMonths(1) : new DateTime(namChon + 1, 1, 1);

            var donHangs = await _context.DonHangs
                .AsNoTracking()
                .Include(d => d.ChiTietDonHangs)
                    .ThenInclude(c => c.MaSkuNavigation)
                        .ThenInclude(s => s!.MaMauSanPhamNavigation)
                .Include(d => d.MaNhanVienNavigation)
                .Where(d => d.NgayDatHang >= tu && d.NgayDatHang < den)
                .OrderBy(d => d.NgayDatHang)
                .ToListAsync();

            var list = new List<DonHangChiTietVm>();
            foreach (var d in donHangs)
            {
                var sanPhams = d.ChiTietDonHangs
                    .Select(c => c.MaSkuNavigation?.MaMauSanPhamNavigation?.TenSanPham ?? c.MaSkuNavigation?.MaCodeSku ?? "—")
                    .Distinct()
                    .ToList();
                list.Add(new DonHangChiTietVm
                {
                    MaDonHang = d.MaDonHang,
                    MaCodeDonHang = d.MaCodeDonHang,
                    NgayDat = d.NgayDatHang ?? DateTime.MinValue,
                    KhachHang = d.TenKhachHangGiao ?? "—",
                    DienThoai = d.SdtKhachHangGiao ?? "—",
                    NhanVien = d.MaNhanVienNavigation?.HoTen ?? "—",
                    HinhThuc = d.HinhThucNhanHang == "GIAO_HANG" ? "Giao hàng" : "Tại shop",
                    SoDong = d.ChiTietDonHangs.Count,
                    SoLuong = d.ChiTietDonHangs.Sum(c => c.SoLuong),
                    TongHang = d.TongTienHang,
                    GiamGia = d.TienGiamGia,
                    PhiVanChuyen = d.PhiVanChuyen,
                    ThanhTien = d.ThanhTien,
                    TrangThai = TrangThaiDon(d.TrangThai),
                    ThanhToan = d.TrangThaiThanhToan == "PAID" ? "Đã thanh toán" : "Chưa thanh toán",
                    SanPham = string.Join(", ", sanPhams)
                });
            }
            return list;
        }

        private async Task<List<PhieuNhapChiTietVm>> LayPhieuNhapTrongThangAsync(int namChon, int thang)
        {
            var tu = new DateTime(namChon, thang, 1);
            var den = tu.AddMonths(1);

            var phieus = await _context.PhieuNhapKhos
                .AsNoTracking()
                .Include(p => p.MaNhaCungCapNavigation)
                .Include(p => p.NguoiTaoNavigation)
                .Include(p => p.ChiTietPhieuNhapKhos)
                .Where(p => p.NgayNhap >= tu && p.NgayNhap < den)
                .OrderBy(p => p.NgayNhap)
                .ToListAsync();

            return phieus.Select(p => new PhieuNhapChiTietVm
            {
                MaPhieuNhap = p.MaPhieuNhap,
                MaCodePhieuNhap = p.MaCodePhieuNhap,
                NgayNhap = p.NgayNhap ?? DateTime.MinValue,
                NhaCungCap = p.MaNhaCungCapNavigation?.TenNhaCungCap ?? "—",
                NguoiTao = p.NguoiTaoNavigation?.HoTen ?? "—",
                SoDong = p.ChiTietPhieuNhapKhos.Count,
                SoLuong = p.ChiTietPhieuNhapKhos.Sum(c => c.SoLuongThucNhan),
                TongTien = p.TongTienHang ?? 0m,
                TrangThai = p.TrangThai switch
                {
                    "COMPLETED" => "Hoàn tất",
                    "CANCELLED" => "Đã hủy",
                    "RECEIVING" => "Đang nhận",
                    _ => "Nháp"
                }
            }).ToList();
        }

        private static string TrangThaiDon(string? trangThai) => trangThai switch
        {
            "COMPLETED" => "Hoàn tất",
            "CANCELLED" => "Đã hủy",
            "CONFIRMED" => "Đã xác nhận",
            _ => "Chờ xử lý"
        };
    }
}
