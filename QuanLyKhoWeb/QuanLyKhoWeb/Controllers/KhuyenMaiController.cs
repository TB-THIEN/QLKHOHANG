using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Models.Entities;
using QuanLyKhoWeb.Models.ViewModels;

namespace QuanLyKhoWeb.Controllers
{
    public class KhuyenMaiController : Controller
    {
        private readonly DoAnQlkContext _context;
        public KhuyenMaiController(DoAnQlkContext context) => _context = context;

        // Danh sách mã (chỉ Mã khuyến mãi -> giảm giá đơn hàng)
        public async Task<IActionResult> Index(string? search)
        {
            ViewData["SearchValue"] = search;
            var q = _context.KhuyenMais.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(k => k.MaCodeKhuyenMai.Contains(search) || k.TenKhuyenMai.Contains(search));
            return View(await q.OrderByDescending(k => k.MaKhuyenMai).Take(200).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var km = await _context.KhuyenMais.FindAsync(id);
            if (km == null) return NotFound();
            return View(km);
        }

        // Tạo/sửa/bật-tắt/xóa mã: Quản lý + NV kho + NV bán hàng (chặn NV giao hàng).
        // Xem danh sách/chi tiết thì mọi tài khoản đăng nhập đều được.
        [HttpGet, Authorize(Policy = "DuocTao")]
        public async Task<IActionResult> Create()
        {
            // Mã tự sinh, chỉ hiển thị trước để biết (không nhập tay)
            ViewBag.MaTuSinh = await SinhMaKeTiep();
            return View(new KhuyenMaiCreateVm());
        }

        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(KhuyenMaiCreateVm vm)
        {
            // Mã KM do server tự sinh, bỏ qua mọi giá trị gửi lên từ form
            vm.MaCodeKhuyenMai = await SinhMaKeTiep();
            KiemTraGiaTriGiam(vm, null);
            QuanLyKhoWeb.Services.KiemTraDauVao.BaoLoiSoNguyen(ModelState, nameof(vm.SoLanSuDungToiDa),
                "Giới hạn sử dụng phải là số nguyên lớn hơn 0 (không nhập chữ hay số thập phân).",
                nameof(vm.SoLanSuDungToiDa));
            QuanLyKhoWeb.Services.KiemTraDauVao.BaoLoiSoNguyen(ModelState, nameof(vm.GiaTriGiam),
                "Giá trị giảm phải là số (không nhập chữ).",
                nameof(vm.GiaTriGiam));
            QuanLyKhoWeb.Services.KiemTraDauVao.BaoLoiSoNguyen(ModelState, nameof(vm.GiaTriDonToiThieu),
                "Giá trị đơn tối thiểu phải là số (không nhập chữ).",
                nameof(vm.GiaTriDonToiThieu));
            if (vm.NgayKetThuc <= vm.NgayBatDau)
                ModelState.AddModelError(nameof(vm.NgayKetThuc), "Ngày kết thúc phải sau ngày bắt đầu (bắt buộc nhập).");
            if (!QuanLyKhoWeb.Services.KiemTraDauVao.LaTenHopLe(vm.TenKhuyenMai))
                ModelState.AddModelError(nameof(vm.TenKhuyenMai), "Tên mã không được chứa số.");
            if (!ModelState.IsValid)
            {
                ViewBag.MaTuSinh = vm.MaCodeKhuyenMai;
                return View(vm);
            }

            _context.KhuyenMais.Add(new KhuyenMai
            {
                MaCodeKhuyenMai = vm.MaCodeKhuyenMai.Trim(),
                TenKhuyenMai = vm.TenKhuyenMai.Trim(),
                LoaiGiamGia = vm.LoaiGiamGia, // chỉ giảm giá đơn hàng
                GiaTriGiam = vm.GiaTriGiam!.Value,
                GiaTriDonToiThieu = vm.GiaTriDonToiThieu,
                SoLanSuDungToiDa = vm.SoLanSuDungToiDa, // nhập số lần, bỏ checkbox/mỗi khách 1 lần
                SoLanDaDung = 0,
                NgayBatDau = vm.NgayBatDau,
                NgayKetThuc = vm.NgayKetThuc,
                TrangThaiHoatDong = true,
                NgayTao = DateTime.Now
            });
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã tạo mã {vm.MaCodeKhuyenMai}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DoiTrangThai(int id)
        {
            var km = await _context.KhuyenMais.FindAsync(id);
            if (km == null) return NotFound();
            km.TrangThaiHoatDong = !(km.TrangThaiHoatDong ?? true);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = "Đã cập nhật trạng thái mã.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet, Authorize(Policy = "DuocTao")]
        public async Task<IActionResult> Edit(int id)
        {
            var km = await _context.KhuyenMais.FindAsync(id);
            if (km == null) return NotFound();
            return View(new KhuyenMaiCreateVm
            {
                MaCodeKhuyenMai = km.MaCodeKhuyenMai,
                TenKhuyenMai = km.TenKhuyenMai,
                LoaiGiamGia = km.LoaiGiamGia ?? "PERCENT",
                GiaTriGiam = km.GiaTriGiam,
                GiaTriDonToiThieu = km.GiaTriDonToiThieu ?? 0,
                SoLanSuDungToiDa = km.SoLanSuDungToiDa,
                NgayBatDau = km.NgayBatDau,
                NgayKetThuc = km.NgayKetThuc
            });
        }

        // Sửa mã: mã code chốt từ lúc tạo (đơn hàng cũ tham chiếu theo id nhưng giữ
        // code ổn định để khỏi nhầm); giới hạn mới không được thấp hơn số đã dùng.
        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, KhuyenMaiCreateVm vm)
        {
            var km = await _context.KhuyenMais.FindAsync(id);
            if (km == null) return NotFound();
            if (vm.NgayKetThuc <= vm.NgayBatDau)
                ModelState.AddModelError(nameof(vm.NgayKetThuc), "Ngày kết thúc phải sau ngày bắt đầu (bắt buộc nhập).");
            KiemTraGiaTriGiam(vm, km.SoLanDaDung);
            QuanLyKhoWeb.Services.KiemTraDauVao.BaoLoiSoNguyen(ModelState, nameof(vm.SoLanSuDungToiDa),
                "Giới hạn sử dụng phải là số nguyên lớn hơn 0 (không nhập chữ hay số thập phân).",
                nameof(vm.SoLanSuDungToiDa));
            QuanLyKhoWeb.Services.KiemTraDauVao.BaoLoiSoNguyen(ModelState, nameof(vm.GiaTriGiam),
                "Giá trị giảm phải là số (không nhập chữ).",
                nameof(vm.GiaTriGiam));
            QuanLyKhoWeb.Services.KiemTraDauVao.BaoLoiSoNguyen(ModelState, nameof(vm.GiaTriDonToiThieu),
                "Giá trị đơn tối thiểu phải là số (không nhập chữ).",
                nameof(vm.GiaTriDonToiThieu));
            if (!QuanLyKhoWeb.Services.KiemTraDauVao.LaTenHopLe(vm.TenKhuyenMai))
                ModelState.AddModelError(nameof(vm.TenKhuyenMai), "Tên mã không được chứa số.");
            if (vm.SoLanSuDungToiDa.HasValue && vm.SoLanSuDungToiDa < km.SoLanDaDung)
                ModelState.AddModelError(nameof(vm.SoLanSuDungToiDa), $"Giới hạn không được thấp hơn số đã dùng ({km.SoLanDaDung}).");
            if (!ModelState.IsValid) return View(vm);

            km.TenKhuyenMai = vm.TenKhuyenMai.Trim();
            km.LoaiGiamGia = vm.LoaiGiamGia;
            km.GiaTriGiam = vm.GiaTriGiam!.Value;
            km.GiaTriDonToiThieu = vm.GiaTriDonToiThieu;
            km.SoLanSuDungToiDa = vm.SoLanSuDungToiDa;
            km.NgayBatDau = vm.NgayBatDau;
            km.NgayKetThuc = vm.NgayKetThuc;
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã cập nhật mã {km.MaCodeKhuyenMai}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Xóa: chặn khi đã có đơn hàng dùng mã (giữ lịch sử tính tiền đơn cũ)
        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Xoa(int id)
        {
            var km = await _context.KhuyenMais
                .Include(k => k.DonHangs)
                .FirstOrDefaultAsync(k => k.MaKhuyenMai == id);
            if (km == null) return NotFound();
            if (km.DonHangs.Any())
            {
                TempData["Loi"] = $"Không thể xóa {km.MaCodeKhuyenMai} vì đã có {km.DonHangs.Count} đơn hàng dùng mã này.";
                return RedirectToAction(nameof(Details), new { id });
            }
            _context.KhuyenMais.Remove(km);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = $"Đã xóa mã {km.MaCodeKhuyenMai}.";
            return RedirectToAction(nameof(Index));
        }

        // Mã KM tự sinh dạng KM-001, KM-002... (tăng đến khi hết trùng)
        private async Task<string> SinhMaKeTiep()
        {
            var ds = await _context.KhuyenMais.Select(k => k.MaCodeKhuyenMai).ToListAsync();
            var so = ds.Count + 1;
            string ma;
            do { ma = $"KM-{so:000}"; so++; } while (ds.Contains(ma));
            return ma;
        }

        // Điều kiện giảm giá: PERCENT chỉ nhận số nguyên 1-100; FIXED_AMOUNT nhập số tiền > 0
        private void KiemTraGiaTriGiam(KhuyenMaiCreateVm vm, int? daDung)
        {
            if (vm.GiaTriGiam == null) return; // để [Required] báo, tránh lỗi null
            var gt = vm.GiaTriGiam.Value;
            if (vm.LoaiGiamGia == "PERCENT")
            {
                if (gt < 1 || gt > 100 || gt != Math.Floor(gt))
                    ModelState.AddModelError(nameof(vm.GiaTriGiam), "Phần trăm giảm phải là số nguyên từ 1 đến 100.");
            }
            else if (vm.LoaiGiamGia == "FIXED_AMOUNT")
            {
                if (gt <= 0)
                    ModelState.AddModelError(nameof(vm.GiaTriGiam), "Số tiền giảm phải lớn hơn 0.");
            }
            else
            {
                ModelState.AddModelError(nameof(vm.LoaiGiamGia), "Vui lòng chọn loại giảm giá.");
            }
        }
    }
}
