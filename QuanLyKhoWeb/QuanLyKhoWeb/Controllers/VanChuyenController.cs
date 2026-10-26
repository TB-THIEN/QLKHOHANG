using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;

namespace QuanLyKhoWeb.Controllers
{
    public class VanChuyenController : Controller
    {
        private readonly DoAnQlkContext _context;
        public VanChuyenController(DoAnQlkContext context) => _context = context;

        // Danh sách vận đơn: Mã đơn hàng, tên hàng, số lượng, họ tên KH, Nhân viên giao (Add hoặc tên)
        public async Task<IActionResult> Index(string? search, DateTime? tuNgay, DateTime? denNgay, int? maNvGiao)
        {
            ViewData["SearchValue"] = search;
            ViewBag.TuNgay = tuNgay?.ToString("yyyy-MM-dd");
            ViewBag.DenNgay = denNgay?.ToString("yyyy-MM-dd");
            ViewBag.MaNvGiao = maNvGiao;

            // Listdown nhân viên giao (thay "Tất cả chi nhánh")
            ViewBag.NvGiaoHang = await _context.NguoiDungs
                .Include(n => n.MaVaiTroNavigation)
                .Where(n => n.MaVaiTroNavigation.DuocNhanVanDon == true && n.TrangThaiHoatDong == true)
                .OrderBy(n => n.HoTen).ToListAsync();

            var q = _context.VanDons
                .Include(v => v.MaDonHangNavigation).ThenInclude(d => d.ChiTietDonHangs).ThenInclude(ct => ct.MaSkuNavigation).ThenInclude(s => s.MaMauSanPhamNavigation)
                .Include(v => v.MaNhanVienGiaoHangNavigation)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(v => v.MaDonHangNavigation.MaCodeDonHang.Contains(search)
                    || v.MaCodeVanDon.Contains(search)
                    || (v.MaDonHangNavigation.TenKhachHangGiao ?? "").Contains(search));

            if (tuNgay.HasValue) q = q.Where(v => v.NgayTao >= tuNgay.Value.Date);
            if (denNgay.HasValue) q = q.Where(v => v.NgayTao < denNgay.Value.Date.AddDays(1));
            if (maNvGiao.HasValue) q = q.Where(v => v.MaNhanVienGiaoHang == maNvGiao.Value);

            var list = await q.OrderByDescending(v => v.NgayTao).Take(200).ToListAsync();
            return View(list);
        }

        // Bấm "Chưa hoàn thành"/"Xem chi tiết" -> xem toàn bộ thông tin đơn (readonly)
        // + điền thông tin giao hàng. Vận đơn đã giao thì khóa, không cho sửa.
        [HttpGet]
        public async Task<IActionResult> GanNhanVien(int id)
        {
            var vd = await _context.VanDons
                .Include(v => v.MaDonHangNavigation).ThenInclude(d => d.ChiTietDonHangs).ThenInclude(ct => ct.MaSkuNavigation).ThenInclude(s => s.MaMauSanPhamNavigation)
                .Include(v => v.MaDonHangNavigation).ThenInclude(d => d.MaKhuyenMaiNavigation)
                .Include(v => v.MaNhanVienGiaoHangNavigation)
                .FirstOrDefaultAsync(v => v.MaVanDon == id);
            if (vd == null) return NotFound();

            ViewBag.NvGiaoHang = await _context.NguoiDungs
                .Include(n => n.MaVaiTroNavigation)
                .Where(n => n.MaVaiTroNavigation.DuocNhanVanDon == true && n.TrangThaiHoatDong == true)
                .OrderBy(n => n.HoTen).ToListAsync();
            return View(vd);
        }

        // Lưu thông tin giao hàng: người nhận + NV giao + ghi chú vận đơn.
        // Vận đơn CHUA_HOAN_THANH -> lưu xong chuyển CHUA_GIAO. Đã giao thì khóa.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GanNhanVien(int id, int maNvGiao, string? tenKhachHang,
            string? sdtKhachHang, string? diaChiGiaoHang, string? ghiChu)
        {
            var vd = await _context.VanDons
                .Include(v => v.MaDonHangNavigation)
                .FirstOrDefaultAsync(v => v.MaVanDon == id);
            if (vd == null) return NotFound();
            if (vd.TrangThai == "DA_GIAO")
            {
                TempData["Loi"] = "Vận đơn đã giao nên không thể thay đổi thông tin.";
                return RedirectToAction(nameof(Index));
            }

            tenKhachHang = tenKhachHang?.Trim();
            sdtKhachHang = sdtKhachHang?.Trim();
            diaChiGiaoHang = diaChiGiaoHang?.Trim();
            if (string.IsNullOrWhiteSpace(tenKhachHang) || !QuanLyKhoWeb.Services.KiemTraDauVao.LaTenHopLe(tenKhachHang))
            {
                TempData["Loi"] = "Tên người nhận không hợp lệ (không được để trống hoặc chứa số).";
                return RedirectToAction(nameof(GanNhanVien), new { id });
            }
            if (string.IsNullOrWhiteSpace(sdtKhachHang) || string.IsNullOrWhiteSpace(diaChiGiaoHang))
            {
                TempData["Loi"] = "Vui lòng nhập đầy đủ số điện thoại và địa chỉ giao hàng.";
                return RedirectToAction(nameof(GanNhanVien), new { id });
            }
            if (maNvGiao <= 0)
            {
                TempData["Loi"] = "Vui lòng chọn nhân viên giao hàng.";
                return RedirectToAction(nameof(GanNhanVien), new { id });
            }
            var nvHopLe = await _context.NguoiDungs
                .Include(n => n.MaVaiTroNavigation)
                .AnyAsync(n => n.MaNguoiDung == maNvGiao
                    && n.TrangThaiHoatDong == true
                    && n.MaVaiTroNavigation.DuocNhanVanDon == true);
            if (!nvHopLe)
            {
                TempData["Loi"] = "Nhân viên giao hàng không hợp lệ hoặc đã ngừng hoạt động.";
                return RedirectToAction(nameof(GanNhanVien), new { id });
            }

            try
            {
                vd.MaNhanVienGiaoHang = maNvGiao;
                vd.GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();
                vd.NgayCapNhat = DateTime.Now;

                var dh = vd.MaDonHangNavigation;
                dh.TenKhachHangGiao = tenKhachHang;
                dh.SdtKhachHangGiao = sdtKhachHang;
                dh.DiaChiGiaoHang = diaChiGiaoHang;

                if (vd.TrangThai == "CHUA_HOAN_THANH")
                    vd.TrangThai = "CHUA_GIAO";

                await _context.SaveChangesAsync();
                TempData["ThongBao"] = vd.TrangThai == "CHUA_GIAO"
                    ? "Đã lưu thông tin vận đơn. Trạng thái: Chưa giao."
                    : "Đã cập nhật thông tin vận đơn.";
            }
            catch (DbUpdateException ex)
            {
                TempData["Loi"] = ex.InnerException?.Message ?? ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        // Xác nhận đã giao: chỉ đi một chiều CHUA_GIAO -> DA_GIAO (đã giao thì khóa,
        // không đổi lại được). Chỉ Quản lý, Nhân viên giao hàng, Nhân viên bán hàng.
        // Vận đơn phải hoàn thành thông tin và có NV giao trước. Trigger cập nhật
        // đơn cũ còn CONFIRMED sang hoàn tất.
        [HttpPost, Authorize(Policy = "DuocXacNhanGiao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhatTrangThai(int id, string trangThai)
        {
            if (trangThai != "DA_GIAO")
            {
                TempData["Loi"] = "Trạng thái không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }
            var vd = await _context.VanDons.FindAsync(id);
            if (vd == null) return NotFound();
            if (vd.TrangThai == "DA_GIAO")
            {
                TempData["Loi"] = "Vận đơn này đã được xác nhận giao trước đó.";
                return RedirectToAction(nameof(Index));
            }
            if (vd.TrangThai == "CHUA_HOAN_THANH")
            {
                TempData["Loi"] = "Vui lòng hoàn thành thông tin vận đơn trước khi xác nhận đã giao.";
                return RedirectToAction(nameof(GanNhanVien), new { id });
            }
            if (vd.MaNhanVienGiaoHang == null)
            {
                TempData["Loi"] = "Vui lòng gán nhân viên giao hàng trước khi xác nhận đã giao.";
                return RedirectToAction(nameof(GanNhanVien), new { id });
            }
            vd.TrangThai = "DA_GIAO";
            vd.NgayCapNhat = DateTime.Now;
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = "Đã xác nhận giao hàng. Vận đơn khóa ở trạng thái Đã giao.";
            return RedirectToAction(nameof(Index));
        }

        // Xóa vận đơn: chỉ xóa được khi đã giao (dọn bản ghi vận chuyển; đơn hàng
        // giữ nguyên COMPLETED để còn lịch sử bán hàng).
        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Xoa(int id)
        {
            var vd = await _context.VanDons.FindAsync(id);
            if (vd == null) return NotFound();
            if (vd.TrangThai != "DA_GIAO")
            {
                TempData["Loi"] = "Chỉ xóa được vận đơn đã giao. Vận đơn chưa giao phải xác nhận đã giao trước.";
                return RedirectToAction(nameof(Index));
            }
            _context.VanDons.Remove(vd);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã xóa vận đơn {vd.MaCodeVanDon}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
