using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Models.Entities;
using QuanLyKhoWeb.Services;
using QuanLyKhoWeb.Services.Interfaces;
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace QuanLyKhoWeb.Controllers
{
    public class TonKhoController : Controller
    {
        private readonly DoAnQlkContext _context;
        private readonly IDbConnectionFactory _factory;
        public TonKhoController(DoAnQlkContext context, IDbConnectionFactory factory)
        {
            _context = context;
            _factory = factory;
        }

        // Quản lí kho (tách khỏi Báo cáo): danh sách tồn + bộ lọc
        public async Task<IActionResult> Index(string? search, string loc = "TATCA")
        {
            ViewData["SearchValue"] = search;
            ViewBag.Loc = loc;
            var q = _context.SanPhamSkus
                .Include(s => s.MaMauSanPhamNavigation)
                .Include(s => s.TonKhoKeHangs).ThenInclude(t => t.MaKeHangNavigation).ThenInclude(k => k.MaKhuVucNavigation)
                .AsQueryable();

            // Tìm theo SKU, tên hàng, và cả khu/kệ đang chứa hàng đó.
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(s => s.MaCodeSku.Contains(search)
                    || s.MaMauSanPhamNavigation.TenSanPham.Contains(search)
                    || s.TonKhoKeHangs.Any(t => t.MaKeHangNavigation.TenKeHang.Contains(search)
                        || t.MaKeHangNavigation.MaKhuVucNavigation.TenKhuVuc.Contains(search))
                    || s.TonKhoKhus.Any(t => t.MaKhuVucNavigation.TenKhuVuc.Contains(search)));

            q = loc switch
            {
                "SAPHET" => q.Where(s => s.SoLuong > 0 && s.SoLuong <= 5),
                "HETHANG" => q.Where(s => s.SoLuong == 0),
                "CONHANG" => q.Where(s => s.SoLuong > 0),
                _ => q
            };

            var list = await q.OrderBy(s => s.SoLuong).Take(300).ToListAsync();
            return View(list);
        }

        // Xem trước khi quét: trả tên + hình (chưa +tồn)
        public async Task<IActionResult> XemTruoc(string maCode)
        {
            var s = await _context.SanPhamSkus
                .Include(x => x.MaMauSanPhamNavigation)
                .FirstOrDefaultAsync(x => x.MaCodeSku == maCode);
            if (s == null) return Json(new { timThay = false });
            return Json(new
            {
                timThay = true,
                maCodeSku = s.MaCodeSku,
                ten = s.MaMauSanPhamNavigation.TenSanPham,
                hinh = s.MaMauSanPhamNavigation.DuongDanHinhAnh,
                tonHienTai = s.SoLuong
            });
        }

        // Bấm Thêm -> +1 tồn (đã có thì +1), Hủy -> không làm gì. Log QUET_MA.
        // Chỉ Quản lý + NV kho được quét +tồn (NV bán hàng và NV giao hàng chỉ xem).
        [HttpPost, Authorize(Policy = "DuocNhapKho"), ValidateAntiForgeryToken]
        public async Task<IActionResult> QuetMa(string maCodeSku)
        {
            var maNguoi = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
            try
            {
                using var conn = (SqlConnection)_factory.CreateConnection();
                var p = new DynamicParameters();
                p.Add("@ma_code_sku", maCodeSku);
                p.Add("@ma_nguoi_thuc_hien", maNguoi);
                await conn.OpenAsync();
                var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "sp_QuetSkuNhapKho", p, commandType: CommandType.StoredProcedure);
                int tonSau = row?.ton_kho_sau_khi_them ?? 0;
                return Json(new { thanhCong = true, tonSau });
            }
            catch (SqlException ex)
            {
                return Json(new { thanhCong = false, thongBao = ex.Message });
            }
        }

        // Xuất báo cáo tồn kho ra Excel (.xlsx) theo bộ lọc hiện tại.
        // Cấu trúc: khối tổng quan (KPI) -> bảng chi tiết có cột trạng thái tồn
        // -> dòng tổng cộng -> tổng hợp theo trạng thái -> ô chữ ký.
        public async Task<IActionResult> XuatExcel(string loc = "TATCA")
        {
            var (list, nhanLoc) = await LayTonKhoAsync(loc);
            var vi = ExcelReportBuilder.Vi;

            var b = new ExcelReportBuilder(
                new double[] { 5, 19, 27, 15, 13, 13, 12, 11, 10, 30 },
                "cái")
            {
                TenSheet = "TonKho_" + DateTime.Today.ToString("yyyyMMdd")
            };

            var tongMatHang = list.Count;
            var tongTon = list.Sum(x => x.SoLuong);
            var tongGiaTri = list.Sum(x => x.GiaTien * x.SoLuong);
            var soHetHang = list.Count(x => x.SoLuong == 0);
            var soSapHet = list.Count(x => x.SoLuong > 0 && x.SoLuong <= 5);
            var soConHang = list.Count(x => x.SoLuong > 5);
            var soChuaXepKe = list.Count(x => !x.TonKhoKeHangs.Any());

            b.TieuDeHanhChinh(
                "BÁO CÁO TỒN KHO",
                "PHÒNG KINH DOANH - KHO VẬN",
                ("Phạm vi lọc", nhanLoc),
                ("Thời điểm chốt", DateTime.Now.ToString("HH:mm 'ngày' dd/MM/yyyy", vi)),
                ("Đơn vị đo", "Cái / sản phẩm"));

            // ---- Khối tổng quan ----
            b.AddRow(new() { [0] = ("TỔNG QUAN TỒN KHO", OStyle.BoldLeft) });
            b.Empty();
            b.AddRow(new()
            {
                [0] = ("Số mặt hàng", OStyle.BoldLeft), [1] = (tongMatHang, OStyle.Integer),
                [3] = ("Tổng số lượng tồn", OStyle.BoldLeft), [4] = (tongTon, OStyle.Integer),
                [6] = ("Giá trị tồn kho", OStyle.BoldLeft), [7] = ((double)tongGiaTri, OStyle.Money)
            });
            b.AddRow(new()
            {
                [0] = ("Còn hàng (> 5)", OStyle.BoldLeft), [1] = (soConHang, OStyle.Integer),
                [3] = ("Sắp hết (1 - 5)", OStyle.BoldLeft), [4] = (soSapHet, OStyle.Integer),
                [6] = ("Hết hàng (= 0)", OStyle.BoldLeft), [7] = (soHetHang, OStyle.Integer)
            });
            b.AddRow(new()
            {
                [0] = ("Chưa xếp kệ", OStyle.BoldLeft), [1] = (soChuaXepKe, OStyle.Integer)
            });
            b.Empty(2);

            // ---- Bảng chi tiết ----
            b.AddRow(new() { [0] = ("CHI TIẾT TỒN THEO SẢN PHẨM", OStyle.BoldLeft) });
            b.Empty();
            b.Header(new[]
            {
                "STT", "Mã SKU", "Tên sản phẩm", "Danh mục", "Nhãn hiệu",
                "Giá tiền", "Giá trị tồn", "Tồn kho", "Trạng thái", "Vị trí kệ"
            });

            int stt = 1;
            foreach (var s in list)
            {
                var vitri = s.TonKhoKeHangs.Any()
                    ? string.Join("; ", s.TonKhoKeHangs.Select(t =>
                        $"{t.MaKeHangNavigation?.MaKhuVucNavigation?.TenKhuVuc} / {t.MaKeHangNavigation?.TenKeHang}"))
                    : "Chưa xếp kệ";

                var trangThai = s.SoLuong == 0 ? "Hết hàng" : s.SoLuong <= 5 ? "Sắp hết" : "Còn hàng";

                b.AddRow(new()
                {
                    [0] = (stt++, OStyle.Integer),
                    [1] = (s.MaCodeSku, OStyle.TextCenter),
                    [2] = (s.MaMauSanPhamNavigation?.TenSanPham ?? "—", OStyle.TextLeft),
                    [3] = (s.MaMauSanPhamNavigation?.MaDanhMucNavigation?.TenDanhMuc ?? "—", OStyle.TextLeft),
                    [4] = (s.MaMauSanPhamNavigation?.NhanHieu ?? "—", OStyle.TextLeft),
                    [5] = ((double)s.GiaTien, OStyle.Money),
                    [6] = ((double)(s.GiaTien * s.SoLuong), OStyle.Money),
                    [7] = (s.SoLuong, OStyle.IntegerBold),
                    [8] = (trangThai, OStyle.TextCenter),
                    [9] = (vitri, OStyle.TextLeft)
                });
            }

            if (list.Count == 0)
            {
                b.AddRow(new() { [0] = ("Không có sản phẩm nào khớp bộ lọc hiện tại.", OStyle.TextCenter) });
            }
            else
            {
                b.AddRow(new()
                {
                    [0] = ("TỔNG CỘNG", OStyle.TotalCenter),
                    [1] = ($"{tongMatHang} mặt hàng", OStyle.TotalCenter),
                    [2] = ("", OStyle.TotalLeft),
                    [3] = ("", OStyle.TotalLeft),
                    [4] = ("", OStyle.TotalLeft),
                    [5] = ("", OStyle.TotalNumber),
                    [6] = (tongGiaTri.ToString("N0", vi), OStyle.TotalNumber),
                    [7] = (tongTon.ToString("N0", vi), OStyle.TotalNumber),
                    [8] = ("", OStyle.TotalCenter),
                    [9] = ("", OStyle.TotalLeft)
                });

                // ---- Tổng hợp theo trạng thái tồn ----
                b.Empty(2);
                b.AddRow(new() { [0] = ("TỔNG HỢP THEO TRẠNG THÁI", OStyle.BoldLeft) });
                b.Empty();
                b.Header(new[] { "Trạng thái", "Số SKU", "Số lượng tồn", "Giá trị tồn" });

                foreach (var nhom in new[]
                {
                    ("Còn hàng (> 5)", list.Where(x => x.SoLuong > 5)),
                    ("Sắp hết (1 - 5)", list.Where(x => x.SoLuong > 0 && x.SoLuong <= 5)),
                    ("Hết hàng (= 0)", list.Where(x => x.SoLuong == 0))
                })
                {
                    var ds = nhom.Item2.ToList();
                    b.AddRow(new()
                    {
                        [0] = (nhom.Item1, OStyle.TextLeft),
                        [1] = (ds.Count.ToString("N0", vi), OStyle.TextCenter),
                        [2] = (ds.Sum(x => x.SoLuong).ToString("N0", vi), OStyle.TextCenter),
                        [3] = (ds.Sum(x => x.GiaTien * x.SoLuong).ToString("N0", vi), OStyle.Number)
                    });
                }
            }

            b.GhiChuNguon("Số liệu tồn kho trong cơ sở dữ liệu nội bộ, chốt tại thời điểm xuất file.");
            b.ChuKy("NGƯỜI LẬP BÁO CÁO", "THỦ KHO");
            return File(b.Build(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"BaoCaoTonKho_{DateTime.Today:yyyyMMdd}_{loc}.xlsx");
        }

        private async Task<(List<SanPhamSku> list, string nhanLoc)> LayTonKhoAsync(string loc)
        {
            var q = _context.SanPhamSkus
                .Include(s => s.MaMauSanPhamNavigation).ThenInclude(m => m.MaDanhMucNavigation)
                .Include(s => s.TonKhoKeHangs).ThenInclude(t => t.MaKeHangNavigation).ThenInclude(k => k.MaKhuVucNavigation)
                .AsQueryable();

            string nhan = loc switch
            {
                "SAPHET" => "Sắp hết (1–5)",
                "HETHANG" => "Hết hàng (= 0)",
                "CONHANG" => "Còn hàng (> 0)",
                _ => "Tất cả sản phẩm"
            };

            q = loc switch
            {
                "SAPHET" => q.Where(s => s.SoLuong > 0 && s.SoLuong <= 5),
                "HETHANG" => q.Where(s => s.SoLuong == 0),
                "CONHANG" => q.Where(s => s.SoLuong > 0),
                _ => q
            };

            var list = await q.OrderBy(s => s.MaCodeSku).Take(1000).ToListAsync();
            return (list, nhan);
        }

        // Xuất danh sách phiếu nhập ra Excel (.xlsx), có thể kèm chi tiết từng dòng hàng
        public async Task<IActionResult> XuatExcelPhieuNhap(string? search, int? id = null)
        {
            var vi = ExcelReportBuilder.Vi;

            if (id is int maPhieu)
            {
                var p = await _context.PhieuNhapKhos
                    .Include(x => x.MaNhaCungCapNavigation)
                    .Include(x => x.NguoiTaoNavigation)
                    .Include(x => x.ChiTietPhieuNhapKhos).ThenInclude(c => c.MaSkuNavigation).ThenInclude(s => s.MaMauSanPhamNavigation)
                    .FirstOrDefaultAsync(x => x.MaPhieuNhap == maPhieu);
                if (p == null) return NotFound();

                var b = new ExcelReportBuilder(new[] { 5.0, 16, 24, 13, 9, 9, 12, 13 }, "VNĐ")
                {
                    TenSheet = p.MaCodePhieuNhap.Replace("-", "_")
                };
                b.TieuDeHanhChinh(
                    $"PHIẾU NHẬP KHO {p.MaCodePhieuNhap}",
                    "PHÒNG KINH DOANH - KHO VẬN",
                    ("Nhà cung cấp", p.MaNhaCungCapNavigation?.TenNhaCungCap ?? "—"),
                    ("Người tạo", p.NguoiTaoNavigation?.HoTen ?? "—"),
                    ("Ngày nhập", p.NgayNhap?.ToString("HH:mm 'ngày' dd/MM/yyyy", vi) ?? "—"),
                    ("Trạng thái", TrangThaiPhieu(p.TrangThai)));

                b.Header(new[] { "STT", "Mã SKU", "Tên sản phẩm", "SL kế hoạch", "SL thực nhận", "Đơn giá nhập", "Thành tiền", "" });

                int stt = 1;
                decimal tongTien = 0;
                foreach (var c in p.ChiTietPhieuNhapKhos)
                {
                    var thanhTien = c.ThanhTien ?? (c.DonGiaNhap ?? 0) * c.SoLuongThucNhan;
                    tongTien += thanhTien;
                    b.AddRow(new()
                    {
                        [0] = (stt++, OStyle.Integer),
                        [1] = (c.MaSkuNavigation?.MaCodeSku ?? "—", OStyle.TextCenter),
                        [2] = (c.MaSkuNavigation?.MaMauSanPhamNavigation?.TenSanPham ?? "—", OStyle.TextLeft),
                        [3] = (c.SoLuongKeHoach.ToString("N0", vi), OStyle.TextCenter),
                        [4] = (c.SoLuongThucNhan.ToString("N0", vi), OStyle.NumberBold),
                        [5] = ((c.DonGiaNhap ?? 0).ToString("N0", vi), OStyle.Number),
                        [6] = (thanhTien.ToString("N0", vi), OStyle.Number),
                        [7] = ("", OStyle.Normal)
                    });
                }

                b.AddRow(new()
                {
                    [0] = ("TỔNG CỘNG", OStyle.TotalCenter),
                    [1] = ($"{p.ChiTietPhieuNhapKhos.Count} dòng", OStyle.TotalCenter),
                    [2] = ("", OStyle.TotalLeft),
                    [3] = (p.ChiTietPhieuNhapKhos.Sum(c => c.SoLuongKeHoach).ToString("N0", vi), OStyle.TotalCenter),
                    [4] = (p.ChiTietPhieuNhapKhos.Sum(c => c.SoLuongThucNhan).ToString("N0", vi), OStyle.TotalNumber),
                    [5] = ("", OStyle.TotalNumber),
                    [6] = ((p.TongTienHang ?? tongTien).ToString("N0", vi), OStyle.TotalNumber),
                    [7] = ("", OStyle.TotalLeft)
                });

                b.ChuKy(benPhai: "THỦ KHO");
                return File(b.Build(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"PhieuNhap_{p.MaCodePhieuNhap}.xlsx");
            }

            var q = _context.PhieuNhapKhos
                .Include(x => x.MaNhaCungCapNavigation)
                .Include(x => x.NguoiTaoNavigation)
                .Include(x => x.ChiTietPhieuNhapKhos)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(x => x.MaCodePhieuNhap.Contains(search)
                    || (x.MaNhaCungCapNavigation != null && x.MaNhaCungCapNavigation.TenNhaCungCap.Contains(search)));
            var ds = await q.OrderByDescending(x => x.MaPhieuNhap).Take(1000).ToListAsync();

            var w = new ExcelReportBuilder(new[] { 5.0, 18, 26, 18, 15, 9, 9, 14, 12 }, "VNĐ")
            {
                TenSheet = "PhieuNhap_" + DateTime.Today.ToString("yyyyMMdd")
            };
            w.TieuDeHanhChinh(
                "DANH SÁCH PHIẾU NHẬP KHO",
                "PHÒNG KINH DOANH - KHO VẬN",
                ("Phạm vi", string.IsNullOrWhiteSpace(search) ? "Toàn bộ phiếu nhập" : $"Từ khóa \"{search}\""),
                ("Số phiếu", ds.Count.ToString("N0", vi)),
                ("Tổng giá trị nhập", ds.Sum(x => x.TongTienHang ?? 0).ToString("N0", vi) + " đ"),
                ("Đơn vị tính", "VNĐ"));

            w.Header(new[] { "STT", "Mã phiếu", "Nhà cung cấp", "Người tạo", "Ngày nhập", "Số dòng", "Số lượng", "Tổng tiền", "Trạng thái" });

            int i2 = 1;
            foreach (var p in ds)
            {
                w.AddRow(new()
                {
                    [0] = (i2++.ToString(), OStyle.TextCenter),
                    [1] = (p.MaCodePhieuNhap, OStyle.TextCenter),
                    [2] = (p.MaNhaCungCapNavigation?.TenNhaCungCap ?? "—", OStyle.TextLeft),
                    [3] = (p.NguoiTaoNavigation?.HoTen ?? "—", OStyle.TextLeft),
                    [4] = (p.NgayNhap?.ToString("dd/MM/yyyy HH:mm", vi) ?? "—", OStyle.TextCenter),
                    [5] = (p.ChiTietPhieuNhapKhos.Count.ToString(), OStyle.TextCenter),
                    [6] = (p.ChiTietPhieuNhapKhos.Sum(c => c.SoLuongThucNhan).ToString("N0", vi), OStyle.TextCenter),
                    [7] = ((p.TongTienHang ?? 0).ToString("N0", vi), OStyle.NumberBold),
                    [8] = (TrangThaiPhieu(p.TrangThai), OStyle.TextCenter)
                });
            }

            if (ds.Count == 0)
            {
                w.AddRow(new() { [0] = ("Không có phiếu nhập nào khớp bộ lọc hiện tại.", OStyle.TextCenter) });
            }
            else
            {
                w.AddRow(new()
                {
                    [0] = ("TỔNG CỘNG", OStyle.TotalCenter),
                    [1] = ($"{ds.Count} phiếu", OStyle.TotalCenter),
                    [2] = ("", OStyle.TotalLeft),
                    [3] = ("", OStyle.TotalLeft),
                    [4] = ("", OStyle.TotalCenter),
                    [5] = (ds.Sum(x => x.ChiTietPhieuNhapKhos.Count).ToString("N0", vi), OStyle.TotalCenter),
                    [6] = (ds.Sum(x => x.ChiTietPhieuNhapKhos.Sum(c => c.SoLuongThucNhan)).ToString("N0", vi), OStyle.TotalNumber),
                    [7] = (ds.Sum(x => x.TongTienHang ?? 0).ToString("N0", vi), OStyle.TotalNumber),
                    [8] = ("", OStyle.TotalCenter)
                });
            }

            w.ChuKy(benPhai: "THỦ KHO");
            return File(w.Build(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DanhSachPhieuNhap_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        private static string TrangThaiPhieu(string? trangThai) => trangThai switch
        {
            "COMPLETED" => "Hoàn tất",
            "CANCELLED" => "Đã hủy",
            "RECEIVING" => "Đang nhận",
            _ => "Nháp"
        };

        // ================= PHẦN PHIẾU NHẬP (gộp chung mục Tồn kho & Nhập kho) =================

        // Danh sách phiếu nhập
        public async Task<IActionResult> PhieuNhap(string? search)
        {
            ViewData["SearchValue"] = search;
            var q = _context.PhieuNhapKhos
                .Include(p => p.MaNhaCungCapNavigation)
                .Include(p => p.NguoiTaoNavigation)
                .Include(p => p.ChiTietPhieuNhapKhos)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(p => p.MaCodePhieuNhap.Contains(search)
                    || (p.MaNhaCungCapNavigation != null && p.MaNhaCungCapNavigation.TenNhaCungCap.Contains(search))
                    || (p.NguoiTaoNavigation != null && p.NguoiTaoNavigation.HoTen.Contains(search))
                    || (p.GhiChu != null && p.GhiChu.Contains(search)));
            return View(await q.OrderByDescending(p => p.MaPhieuNhap).Take(200).ToListAsync());
        }

        public async Task<IActionResult> ChiTietPhieu(int id)
        {
            var p = await _context.PhieuNhapKhos
                .Include(x => x.MaNhaCungCapNavigation)
                .Include(x => x.NguoiTaoNavigation)
                .Include(x => x.ChiTietPhieuNhapKhos).ThenInclude(c => c.MaSkuNavigation).ThenInclude(s => s.MaMauSanPhamNavigation)
                .FirstOrDefaultAsync(x => x.MaPhieuNhap == id);
            if (p == null) return NotFound();
            return View(p);
        }

        [HttpGet, Authorize(Policy = "DuocNhapKho")]
        public async Task<IActionResult> TaoPhieu()
        {
            ViewBag.Nccs = await _context.NhaCungCaps
                .Where(n => n.TrangThaiHoatDong == true)
                .OrderBy(n => n.TenNhaCungCap).ToListAsync();
            return View();
        }

        // Tìm SKU để thêm vào phiếu (kèm tồn hiện tại).
        // Không nhập gì -> trả tối đa 60 món để lướt chọn; có từ khóa -> 10 gợi ý khớp nhất
        public async Task<IActionResult> TimSku(string term)
        {
            var layBaoNhieu = string.IsNullOrEmpty(term) ? 60 : 10;
            var list = await _context.SanPhamSkus
                .Include(s => s.MaMauSanPhamNavigation)
                .Where(s => string.IsNullOrEmpty(term) ||
                    s.MaCodeSku.Contains(term) ||
                    s.MaMauSanPhamNavigation.TenSanPham.Contains(term))
                .Take(layBaoNhieu)
                .Select(s => new
                {
                    s.MaSku,
                    s.MaCodeSku,
                    ten = s.MaMauSanPhamNavigation.TenSanPham,
                    ton = s.SoLuong
                }).ToListAsync();
            return Json(list);
        }

        [HttpPost, Authorize(Policy = "DuocNhapKho"), ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoPhieu(int? maNhaCungCap, string? ghiChu,
            List<int> MaSkus, List<int> SoLuongKeHoaches, List<int> SoLuongThucNhans, List<decimal> DonGiaNhaps,
            bool luuNhap = false)
        {
            ViewBag.Nccs = await _context.NhaCungCaps
                .Where(n => n.TrangThaiHoatDong == true)
                .OrderBy(n => n.TenNhaCungCap).ToListAsync();

            if (maNhaCungCap == null || !await _context.NhaCungCaps
                    .AnyAsync(n => n.MaNhaCungCap == maNhaCungCap && n.TrangThaiHoatDong == true))
            {
                ModelState.AddModelError(string.Empty, "Vui lòng chọn nhà cung cấp.");
                return View();
            }
            if (MaSkus == null || !MaSkus.Any())
            {
                ModelState.AddModelError(string.Empty, "Vui lòng thêm ít nhất 1 sản phẩm vào phiếu.");
                return View();
            }
            // Ô số lượng/đơn giá nhập chữ/số thập phân sai chỗ -> báo tiếng Việt
            // thay vì lặng lẽ lấy số mặc định
            QuanLyKhoWeb.Services.KiemTraDauVao.BaoLoiSoNguyen(ModelState, "SoLuongKeHoaches",
                "Số lượng phải là số nguyên lớn hơn hoặc bằng 0 (không nhập chữ hay số thập phân).",
                "SoLuongKeHoaches", "SoLuongThucNhans", "MaSkus");

            var maNguoi = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;
            var maCode = await SinhMaPhieu();
            var phieu = new PhieuNhapKho
            {
                MaCodePhieuNhap = maCode,
                MaNhaCungCap = maNhaCungCap,
                NguoiTao = maNguoi,
                NgayNhap = DateTime.Now,
                TrangThai = "PENDING",
                GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim(),
                NgayTao = DateTime.Now
            };
            for (int i = 0; i < MaSkus.Count; i++)
            {
                var slKh = (SoLuongKeHoaches != null && i < SoLuongKeHoaches.Count) ? SoLuongKeHoaches[i] : 0;
                var slTn = (SoLuongThucNhans != null && i < SoLuongThucNhans.Count) ? SoLuongThucNhans[i] : slKh;
                var dg = (DonGiaNhaps != null && i < DonGiaNhaps.Count) ? DonGiaNhaps[i] : 0;
                if (slKh < 0) slKh = 0;
                if (slTn < 0) slTn = 0;
                if (dg < 0) dg = 0;
                if (!await _context.SanPhamSkus.AnyAsync(s => s.MaSku == MaSkus[i])) continue;
                phieu.ChiTietPhieuNhapKhos.Add(new ChiTietPhieuNhapKho
                {
                    MaSku = MaSkus[i],
                    SoLuongKeHoach = slKh,
                    SoLuongThucNhan = slTn,
                    DonGiaNhap = dg
                });
            }
            if (!phieu.ChiTietPhieuNhapKhos.Any())
            {
                ModelState.AddModelError(string.Empty, "Không có dòng hàng hợp lệ.");
                return View();
            }
            if (!phieu.ChiTietPhieuNhapKhos.Any(ct => ct.SoLuongThucNhan > 0) && !luuNhap)
                ModelState.AddModelError(string.Empty, "Vui lòng nhập số lượng thực nhận lớn hơn 0 cho ít nhất 1 sản phẩm.");
            if (!ModelState.IsValid) return View();

            _context.PhieuNhapKhos.Add(phieu);
            await _context.SaveChangesAsync();

            // Lưu nháp: giữ PENDING, chưa cộng tồn — hoàn tất sau ở trang chi tiết
            if (luuNhap)
            {
                TempData["ThongBao"] = $"Đã lưu nháp phiếu {maCode}. Tồn kho chưa thay đổi, bấm Hoàn tất phiếu khi hàng về.";
                return RedirectToAction(nameof(ChiTietPhieu), new { id = phieu.MaPhieuNhap });
            }

            // Tạo phiếu xong cộng tồn kho ngay (sp ghi nhật ký NHAP_KHO + chuyển COMPLETED).
            try
            {
                using var conn = (SqlConnection)_factory.CreateConnection();
                var p = new DynamicParameters();
                p.Add("@ma_phieu_nhap", phieu.MaPhieuNhap);
                await conn.OpenAsync();
                await conn.ExecuteAsync("sp_HoanTatNhapKho", p, commandType: CommandType.StoredProcedure);
                TempData["ThongBao"] = $"Đã tạo phiếu nhập {maCode} và cộng tồn kho.";
            }
            catch (SqlException ex)
            {
                TempData["Loi"] = $"Đã tạo phiếu {maCode} nhưng chưa cộng được tồn: {ex.Message} Bấm Hoàn tất để thử lại.";
            }
            return RedirectToAction(nameof(ChiTietPhieu), new { id = phieu.MaPhieuNhap });
        }

        // Hoàn tất: +tồn theo số thực nhận, ghi log NHAP_KHO (qua SP).
        // Chỉ dùng cho phiếu nháp cũ/lỗi cộng tồn; phiếu tạo mới đã tự hoàn tất.
        [HttpPost, Authorize(Policy = "DuocNhapKho"), ValidateAntiForgeryToken]
        public async Task<IActionResult> HoanTatPhieu(int id)
        {
            try
            {
                using var conn = (SqlConnection)_factory.CreateConnection();
                var p = new DynamicParameters();
                p.Add("@ma_phieu_nhap", id);
                await conn.OpenAsync();
                await conn.ExecuteAsync("sp_HoanTatNhapKho", p, commandType: CommandType.StoredProcedure);
                TempData["ThongBao"] = "Đã hoàn tất phiếu nhập, tồn kho đã được cộng.";
            }
            catch (SqlException ex)
            {
                TempData["Loi"] = ex.Message;
            }
            return RedirectToAction(nameof(ChiTietPhieu), new { id });
        }

        // Hủy phiếu nháp (đã hoàn tất thì không được hủy để giữ lịch sử tồn)
        [HttpPost, Authorize(Policy = "DuocNhapKho"), ValidateAntiForgeryToken]
        public async Task<IActionResult> HuyPhieu(int id)
        {
            var p = await _context.PhieuNhapKhos.FindAsync(id);
            if (p == null) return NotFound();
            if (p.TrangThai == "COMPLETED")
            {
                TempData["Loi"] = "Phiếu đã hoàn tất, không thể hủy.";
                return RedirectToAction(nameof(ChiTietPhieu), new { id });
            }
            p.TrangThai = "CANCELLED";
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = "Đã hủy phiếu nhập.";
            return RedirectToAction(nameof(ChiTietPhieu), new { id });
        }

        // Xóa vật lý phiếu chưa hoàn tất (dòng chi tiết tự xóa theo CASCADE)
        [HttpPost, Authorize(Policy = "DuocNhapKho"), ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaPhieu(int id)
        {
            var p = await _context.PhieuNhapKhos.FindAsync(id);
            if (p == null) return NotFound();
            if (p.TrangThai == "COMPLETED")
            {
                TempData["Loi"] = "Phiếu đã hoàn tất, không thể xóa để giữ lịch sử tồn kho.";
                return RedirectToAction(nameof(ChiTietPhieu), new { id });
            }
            _context.PhieuNhapKhos.Remove(p);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã xóa phiếu {p.MaCodePhieuNhap}.";
            return RedirectToAction(nameof(PhieuNhap));
        }

        // Mã phiếu dạng PN-yyyyMMdd-### (tăng đến khi hết trùng trong ngày)
        private async Task<string> SinhMaPhieu()
        {
            var dau = "PN-" + DateTime.Today.ToString("yyyyMMdd") + "-";
            var ds = await _context.PhieuNhapKhos
                .Where(p => p.MaCodePhieuNhap.StartsWith(dau))
                .Select(p => p.MaCodePhieuNhap).ToListAsync();
            var so = ds.Count + 1;
            string ma;
            do { ma = $"{dau}{so:000}"; so++; } while (ds.Contains(ma));
            return ma;
        }
    }
}
