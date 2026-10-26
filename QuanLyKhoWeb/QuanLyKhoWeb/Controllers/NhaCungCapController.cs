using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Models.Entities;

namespace QuanLyKhoWeb.Controllers
{
    // Chức năng Nhà cung cấp - Quản lý + Nhân viên kho + Nhân viên bán hàng
    // (chính sách DuocTao chặn Nhân viên giao hàng). Server chặn bằng policy ở
    // cả controller (không chỉ ẩn nút ở giao diện).
    [Authorize(Policy = "DuocTao")]
    public class NhaCungCapController : Controller
    {
        private readonly DoAnQlkContext _context;
        public NhaCungCapController(DoAnQlkContext context) => _context = context;

        // Danh sách + tìm kiếm theo mã/tên/SĐT/email
        public async Task<IActionResult> Index(string? search)
        {
            ViewData["SearchValue"] = search;
            var q = _context.NhaCungCaps.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(n => n.MaCodeNcc.Contains(search)
                    || n.TenNhaCungCap.Contains(search)
                    || (n.SoDienThoai != null && n.SoDienThoai.Contains(search))
                    || (n.Email != null && n.Email.Contains(search)));
            return View(await q.OrderBy(n => n.MaNhaCungCap).Take(200).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var ncc = await _context.NhaCungCaps
                .Include(n => n.PhieuNhapKhos)
                .FirstOrDefaultAsync(n => n.MaNhaCungCap == id);
            if (ncc == null) return NotFound();
            return View(ncc);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Mã tự sinh, chỉ hiển thị trước để quản lý biết (không nhập tay)
            ViewBag.MaTuSinh = await SinhMaKeTiep();
            return View(new NhaCungCap { TrangThaiHoatDong = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NhaCungCap vm)
        {
            // Mã NCC do server tự sinh, bỏ qua mọi giá trị mã gửi lên từ form
            vm.MaCodeNcc = await SinhMaKeTiep();
            vm.TenNhaCungCap = (vm.TenNhaCungCap ?? "").Trim();
            var email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();
            ModelState.Clear();
            if (string.IsNullOrWhiteSpace(vm.TenNhaCungCap))
                ModelState.AddModelError(nameof(vm.TenNhaCungCap), "Vui lòng nhập tên nhà cung cấp.");
            else if (!QuanLyKhoWeb.Services.KiemTraDauVao.LaTenHopLe(vm.TenNhaCungCap))
                ModelState.AddModelError(nameof(vm.TenNhaCungCap), "Tên nhà cung cấp không được chứa số.");
            if (!KiemTraEmail(email, out var loiEmail))
                ModelState.AddModelError(nameof(vm.Email), loiEmail);
            else if (email != null && await _context.NhaCungCaps.AnyAsync(n => n.Email == email))
                ModelState.AddModelError(nameof(vm.Email), "Email này đã được nhà cung cấp khác sử dụng.");
            if (!ModelState.IsValid)
            {
                ViewBag.MaTuSinh = vm.MaCodeNcc;
                return View(vm);
            }

            vm.SoDienThoai = string.IsNullOrWhiteSpace(vm.SoDienThoai) ? null : vm.SoDienThoai.Trim();
            vm.Email = email;
            vm.DiaChi = string.IsNullOrWhiteSpace(vm.DiaChi) ? null : vm.DiaChi.Trim();
            vm.NgayTao = DateTime.Now;
            _context.NhaCungCaps.Add(vm);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã thêm nhà cung cấp {vm.MaCodeNcc}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var ncc = await _context.NhaCungCaps.FindAsync(id);
            if (ncc == null) return NotFound();
            return View(ncc);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, NhaCungCap vm)
        {
            var ncc = await _context.NhaCungCaps.FindAsync(id);
            if (ncc == null) return NotFound();

            // Mã NCC tự sinh từ lúc tạo, không cho sửa (bỏ qua giá trị gửi lên)
            vm.TenNhaCungCap = (vm.TenNhaCungCap ?? "").Trim();
            var email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();
            ModelState.Clear();
            if (string.IsNullOrWhiteSpace(vm.TenNhaCungCap))
                ModelState.AddModelError(nameof(vm.TenNhaCungCap), "Vui lòng nhập tên nhà cung cấp.");
            else if (!QuanLyKhoWeb.Services.KiemTraDauVao.LaTenHopLe(vm.TenNhaCungCap))
                ModelState.AddModelError(nameof(vm.TenNhaCungCap), "Tên nhà cung cấp không được chứa số.");
            if (!KiemTraEmail(email, out var loiEmail))
                ModelState.AddModelError(nameof(vm.Email), loiEmail);
            else if (email != null && await _context.NhaCungCaps.AnyAsync(n => n.Email == email && n.MaNhaCungCap != id))
                ModelState.AddModelError(nameof(vm.Email), "Email này đã được nhà cung cấp khác sử dụng.");
            if (!ModelState.IsValid)
            {
                vm.MaNhaCungCap = id;
                vm.MaCodeNcc = ncc.MaCodeNcc;
                return View(vm);
            }

            ncc.TenNhaCungCap = vm.TenNhaCungCap;
            ncc.SoDienThoai = string.IsNullOrWhiteSpace(vm.SoDienThoai) ? null : vm.SoDienThoai.Trim();
            ncc.Email = email;
            ncc.DiaChi = string.IsNullOrWhiteSpace(vm.DiaChi) ? null : vm.DiaChi.Trim();
            ncc.TrangThaiHoatDong = vm.TrangThaiHoatDong;
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã cập nhật nhà cung cấp {ncc.MaCodeNcc}.";
            return RedirectToAction(nameof(Index));
        }

        // Xóa: chặn khi còn phiếu nhập kho tham chiếu (FK đang SET NULL nên phải chặn tay
        // để khỏi mất thông tin nguồn hàng của phiếu cũ)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Xoa(int id)
        {
            var ncc = await _context.NhaCungCaps
                .Include(n => n.PhieuNhapKhos)
                .FirstOrDefaultAsync(n => n.MaNhaCungCap == id);
            if (ncc == null) return NotFound();
            if (ncc.PhieuNhapKhos.Any())
            {
                TempData["Loi"] = $"Không thể xóa {ncc.MaCodeNcc} vì còn {ncc.PhieuNhapKhos.Count} phiếu nhập kho của nhà cung cấp này.";
                return RedirectToAction(nameof(Index));
            }
            _context.NhaCungCaps.Remove(ncc);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã xóa nhà cung cấp {ncc.MaCodeNcc}.";
            return RedirectToAction(nameof(Index));
        }

        // Sinh mã kế tiếp dạng NCC-001, NCC-002... (tăng đến khi hết trùng,
        // chống trùng khi 2 người cùng thêm lúc xóa mã cũ)
        private async Task<string> SinhMaKeTiep()
        {
            var ds = await _context.NhaCungCaps.Select(n => n.MaCodeNcc).ToListAsync();
            var so = ds.Count + 1;
            string ma;
            do { ma = $"NCC-{so:000}"; so++; } while (ds.Contains(ma));
            return ma;
        }

        // Email được phép bỏ trống; nếu nhập phải đúng cú pháp
        private static bool KiemTraEmail(string? email, out string loi)
        {
            loi = "";
            if (string.IsNullOrWhiteSpace(email)) return true;
            if (email.Contains(' ') || email.Count(c => c == '@') != 1)
            {
                loi = "Email không đúng định dạng. Ví dụ đúng: ten@congty.com";
                return false;
            }
            var phan = email.Split('@');
            if (phan[0].Length == 0 || !phan[1].Contains('.') || phan[1].StartsWith('.') || phan[1].EndsWith('.'))
            {
                loi = "Email không đúng định dạng. Ví dụ đúng: ten@congty.com";
                return false;
            }
            return true;
        }
    }
}
