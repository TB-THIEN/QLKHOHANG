using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Models.Entities;
using QuanLyKhoWeb.Models.ViewModels;
using QuanLyKhoWeb.Services.Interfaces;
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace QuanLyKhoWeb.Controllers
{
    public class KhuHangController : Controller
    {
        private readonly DoAnQlkContext _context;
        private readonly IDbConnectionFactory _factory;
        public KhuHangController(DoAnQlkContext context, IDbConnectionFactory factory)
        {
            _context = context;
            _factory = factory;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var khus = await _context.KhuVucKhos
                .Include(k => k.KeHangs)
                    .ThenInclude(ke => ke.TonKhoKeHangs)
                        .ThenInclude(t => t.MaSkuNavigation)
                            .ThenInclude(s => s.MaMauSanPhamNavigation)
                .Include(k => k.TonKhoKhus)
                    .ThenInclude(t => t.MaSkuNavigation)
                        .ThenInclude(s => s.MaMauSanPhamNavigation)
                .OrderBy(k => k.TenKhuVuc)
                .ToListAsync();

            var vm = new KhuHangIndexVm { Search = search?.Trim() };

            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var s = vm.Search;

                // Kết quả tìm hàng hóa: tìm cả sản phẩm chưa xếp khu/kệ.
                var skus = await _context.SanPhamSkus
                    .Include(x => x.MaMauSanPhamNavigation)
                    .Where(x => x.MaCodeSku.Contains(s) || x.MaMauSanPhamNavigation.TenSanPham.Contains(s))
                    .OrderBy(x => x.MaMauSanPhamNavigation.TenSanPham)
                    .Take(100)
                    .ToListAsync();

                foreach (var sku in skus)
                {
                    var kq = new HangTimKiemVm
                    {
                        MaSku = sku.MaSku,
                        TenSanPham = sku.MaMauSanPhamNavigation?.TenSanPham ?? "",
                        MaCodeSku = sku.MaCodeSku,
                        HinhAnh = sku.MaMauSanPhamNavigation?.DuongDanHinhAnh,
                        GiaNiemYet = sku.GiaNiemYet,
                        SoLuongTon = sku.SoLuong
                    };
                    foreach (var khu in khus)
                    {
                        var slKhu = khu.TonKhoKhus.FirstOrDefault(t => t.MaSku == sku.MaSku)?.SoLuong ?? 0;
                        if (slKhu > 0) kq.ViTri.Add($"{khu.TenKhuVuc} (chưa xếp kệ): {slKhu}");
                        foreach (var ke in khu.KeHangs)
                        {
                            var slKe = ke.TonKhoKeHangs.FirstOrDefault(t => t.MaSku == sku.MaSku)?.SoLuong ?? 0;
                            if (slKe > 0) kq.ViTri.Add($"{khu.TenKhuVuc} / {ke.TenKeHang}: {slKe}");
                        }
                    }
                    vm.KetQua.Add(kq);
                }

                // Sơ đồ chỉ giữ khu/kệ có liên quan tới từ khóa.
                vm.Khus = khus.Where(k =>
                    k.TenKhuVuc.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    k.KeHangs.Any(ke => ke.TenKeHang.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                        ke.TonKhoKeHangs.Any(t => TenSku(t.MaSkuNavigation).Contains(s, StringComparison.OrdinalIgnoreCase))) ||
                    k.TonKhoKhus.Any(t => TenSku(t.MaSkuNavigation).Contains(s, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
            else
            {
                vm.Khus = khus;
            }

            return View(vm);
        }

        private static string TenSku(SanPhamSku? sku)
            => sku == null ? "" : $"{sku.MaCodeSku} {sku.MaMauSanPhamNavigation?.TenSanPham}";

        // Tạo/sửa/xóa khu-kệ và xếp/gỡ hàng: Quản lý + NV kho + NV bán hàng (chặn NV giao hàng).
        // Xem sơ đồ thì mọi tài khoản đăng nhập đều được.
        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoKhu(string tenKhu)
        {
            if (string.IsNullOrWhiteSpace(tenKhu))
            {
                TempData["Loi"] = "Vui lòng nhập tên khu hàng.";
                return RedirectToAction(nameof(Index));
            }
            if (await _context.KhuVucKhos.AnyAsync(k => k.TenKhuVuc == tenKhu))
            {
                TempData["Loi"] = "Tên khu hàng đã tồn tại.";
                return RedirectToAction(nameof(Index));
            }
            _context.KhuVucKhos.Add(new KhuVucKho { TenKhuVuc = tenKhu.Trim(), LoaiKhuVuc = "STORAGE", TrangThaiHoatDong = true });
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã tạo {tenKhu}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoKe(int maKhuVuc, string tenKe)
        {
            if (string.IsNullOrWhiteSpace(tenKe))
            {
                TempData["Loi"] = "Vui lòng nhập tên kệ.";
                return RedirectToAction(nameof(Index));
            }
            if (await _context.KeHangs.AnyAsync(k => k.MaKhuVuc == maKhuVuc && k.TenKeHang == tenKe))
            {
                TempData["Loi"] = "Tên kệ đã tồn tại trong khu này.";
                return RedirectToAction(nameof(Index));
            }
            _context.KeHangs.Add(new KeHang { MaKhuVuc = maKhuVuc, TenKeHang = tenKe.Trim(), NgayTao = DateTime.Now });
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã tạo {tenKe}.";
            return RedirectToAction(nameof(Index));
        }

        // Tìm sản phẩm để thêm vào kệ (hiển thị hình + tên).
        // Không nhập gì -> trả tối đa 60 món để lướt chọn; có từ khóa -> 10 gợi ý khớp nhất
        public async Task<IActionResult> TimSanPham(string term)
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
                    hinh = s.MaMauSanPhamNavigation.DuongDanHinhAnh,
                    s.GiaNiemYet
                }).ToListAsync();
            return Json(list);
        }

        // Xếp hàng lên kệ với số lượng nhập tay: gọi sp_ThemSkuVaoKe để upsert an toàn khi
        // có đụng độ đồng thời và tự ghi nhật ký tồn kho (CHUYEN_KE), thay vì sửa EF trực tiếp.
        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> ThemHangVaoKe(int maKeHang, int maSku, int soLuong = 1)
        {
            if (soLuong <= 0)
            {
                TempData["Loi"] = "Số lượng xếp lên kệ phải lớn hơn 0.";
                return RedirectToAction(nameof(Index));
            }
            var maNguoi = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
            try
            {
                using var conn = (SqlConnection)_factory.CreateConnection();
                var p = new DynamicParameters();
                p.Add("@ma_sku", maSku);
                p.Add("@ma_ke_hang", maKeHang);
                p.Add("@so_luong", soLuong);
                p.Add("@ma_nguoi_thuc_hien", maNguoi);
                await conn.OpenAsync();
                await conn.ExecuteAsync("sp_ThemSkuVaoKe", p, commandType: CommandType.StoredProcedure);
                TempData["ThongBao"] = $"Đã thêm {soLuong} sản phẩm vào kệ.";
            }
            catch (SqlException ex)
            {
                TempData["Loi"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        // Xếp hàng trực tiếp vào khu (dùng khi khu chưa có kệ hoặc không muốn chọn kệ).
        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> ThemHangVaoKhu(int maKhuVuc, int maSku, int soLuong = 1)
        {
            if (soLuong <= 0)
            {
                TempData["Loi"] = "Số lượng xếp vào khu phải lớn hơn 0.";
                return RedirectToAction(nameof(Index));
            }
            var maNguoi = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
            try
            {
                using var conn = (SqlConnection)_factory.CreateConnection();
                var p = new DynamicParameters();
                p.Add("@ma_sku", maSku);
                p.Add("@ma_khu_vuc", maKhuVuc);
                p.Add("@so_luong", soLuong);
                p.Add("@ma_nguoi_thuc_hien", maNguoi);
                await conn.OpenAsync();
                await conn.ExecuteAsync("sp_ThemSkuVaoKhu", p, commandType: CommandType.StoredProcedure);
                TempData["ThongBao"] = $"Đã thêm {soLuong} sản phẩm vào khu.";
            }
            catch (SqlException ex)
            {
                TempData["Loi"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        // Xóa sản phẩm RA KHỎI KHU (chỉ gỡ vị trí xếp, không xóa sản phẩm/tồn kho).
        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaHangKhoiKhu(int maKhuVuc, int maSku)
        {
            var dong = await _context.TonKhoKhus.FindAsync(maSku, maKhuVuc);
            if (dong == null) return NotFound();
            _context.TonKhoKhus.Remove(dong);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = "Đã xóa sản phẩm ra khỏi khu.";
            return RedirectToAction(nameof(Index));
        }

        // Xóa sản phẩm RA KHỎI KỆ (chỉ gỡ vị trí xếp, không xóa sản phẩm khỏi hệ thống,
        // không đổi tổng tồn trên SKU vì hàng vẫn còn trong kho, chỉ chưa xếp kệ)
        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaHangKhoiKe(int maKeHang, int maSku)
        {
            var dong = await _context.TonKhoKeHangs.FindAsync(maSku, maKeHang);
            if (dong == null) return NotFound();
            _context.TonKhoKeHangs.Remove(dong);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = "Đã xóa sản phẩm ra khỏi kệ hàng.";
            return RedirectToAction(nameof(Index));
        }

        // Xóa kệ: chỉ xóa được kệ trống (còn hàng thì từ chối để khỏi mất vị trí tồn)
        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaKe(int maKeHang)
        {
            var ke = await _context.KeHangs
                .Include(k => k.TonKhoKeHangs)
                .FirstOrDefaultAsync(k => k.MaKeHang == maKeHang);
            if (ke == null) return NotFound();
            if (ke.TonKhoKeHangs.Any())
            {
                TempData["Loi"] = $"Không thể xóa {ke.TenKeHang} vì kệ còn hàng. Hãy chuyển hết hàng đi trước.";
                return RedirectToAction(nameof(Index));
            }
            _context.KeHangs.Remove(ke);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã xóa {ke.TenKeHang}.";
            return RedirectToAction(nameof(Index));
        }

        // Xóa khu: chỉ xóa được khu không còn kệ và không còn hàng xếp trực tiếp
        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaKhu(int maKhuVuc)
        {
            var khu = await _context.KhuVucKhos
                .Include(k => k.KeHangs)
                .Include(k => k.TonKhoKhus)
                .FirstOrDefaultAsync(k => k.MaKhuVuc == maKhuVuc);
            if (khu == null) return NotFound();
            if (khu.KeHangs.Any() || khu.TonKhoKhus.Any())
            {
                TempData["Loi"] = $"Không thể xóa {khu.TenKhuVuc} vì còn {khu.KeHangs.Count} kệ và {khu.TonKhoKhus.Count} mặt hàng xếp trực tiếp. Hãy chuyển hết hàng đi trước.";
                return RedirectToAction(nameof(Index));
            }
            _context.KhuVucKhos.Remove(khu);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã xóa {khu.TenKhuVuc}.";
            return RedirectToAction(nameof(Index));
        }
    }
}