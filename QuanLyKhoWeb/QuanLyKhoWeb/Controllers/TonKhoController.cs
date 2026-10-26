using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Models.Entities;
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

            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(s => s.MaCodeSku.Contains(search) || s.MaMauSanPhamNavigation.TenSanPham.Contains(search));

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
        // Chỉ Quản lý + NV kho + NV bán hàng được quét +tồn (chặn NV giao hàng).
        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
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

        // Xuất báo cáo tồn kho ra Excel (CSV) theo bộ lọc hiện tại
        public async Task<IActionResult> XuatExcel(string loc = "TATCA")
        {
            var q = _context.SanPhamSkus
                .Include(s => s.MaMauSanPhamNavigation).ThenInclude(m => m.MaDanhMucNavigation)
                .Include(s => s.TonKhoKeHangs).ThenInclude(t => t.MaKeHangNavigation).ThenInclude(k => k.MaKhuVucNavigation)
                .AsQueryable();

            q = loc switch
            {
                "SAPHET" => q.Where(s => s.SoLuong > 0 && s.SoLuong <= 5),
                "HETHANG" => q.Where(s => s.SoLuong == 0),
                "CONHANG" => q.Where(s => s.SoLuong > 0),
                _ => q
            };

            var list = await q.OrderBy(s => s.MaCodeSku).Take(1000).ToListAsync();

            static string Csv(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Ma SKU,Ten san pham,Danh muc,Gia,Ton,Vi tri ke");
            foreach (var s in list)
            {
                var vitri = s.TonKhoKeHangs.Any()
                    ? string.Join("; ", s.TonKhoKeHangs.Select(t => $"{t.MaKeHangNavigation?.MaKhuVucNavigation?.TenKhuVuc}-{t.MaKeHangNavigation?.TenKeHang}"))
                    : "Chua xep ke";
                sb.AppendLine(string.Join(",",
                    Csv(s.MaCodeSku),
                    Csv(s.MaMauSanPhamNavigation?.TenSanPham),
                    Csv(s.MaMauSanPhamNavigation?.MaDanhMucNavigation?.TenDanhMuc),
                    s.GiaNiemYet.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    s.SoLuong,
                    Csv(vitri)));
            }
            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"BaoCaoTonKho_{DateTime.Today:yyyyMMdd}_{loc}.csv");
        }

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
                    || (p.MaNhaCungCapNavigation != null && p.MaNhaCungCapNavigation.TenNhaCungCap.Contains(search)));
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
            List<int> MaSkus, List<int> SoLuongKeHoaches, List<int> SoLuongThucNhans, List<decimal> DonGiaNhaps)
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
            if (!phieu.ChiTietPhieuNhapKhos.Any(ct => ct.SoLuongThucNhan > 0))
                ModelState.AddModelError(string.Empty, "Vui lòng nhập số lượng thực nhận lớn hơn 0 cho ít nhất 1 sản phẩm.");
            if (!ModelState.IsValid) return View();

            _context.PhieuNhapKhos.Add(phieu);
            await _context.SaveChangesAsync();

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
