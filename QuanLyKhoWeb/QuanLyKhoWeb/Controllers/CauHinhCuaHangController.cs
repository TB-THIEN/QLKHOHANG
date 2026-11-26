using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Models.Entities;

namespace QuanLyKhoWeb.Controllers
{
    // Cấu hình cửa hàng: thông tin "người bán" in trên hóa đơn điện tử và phiếu giao hàng.
    // Bảng chỉ có 1 dòng (ma_cau_hinh = 1) nên chỉ có màn hình sửa, không có thêm/xóa.
    [Authorize(Policy = "SuaCauHinhCuaHang")]
    public class CauHinhCuaHangController : Controller
    {
        private readonly DoAnQlkContext _context;
        public CauHinhCuaHangController(DoAnQlkContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var ch = await LayCauHinhAsync();
            return View(ch);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CauHinhCuaHang vm)
        {
            // Khóa chính luôn là 1, bỏ qua mọi giá trị gửi lên từ form
            vm.MaCauHinh = 1;
            vm.TenCuaHang = (vm.TenCuaHang ?? "").Trim();
            vm.DiaChi = Gon(vm.DiaChi);
            vm.SoDienThoai = Gon(vm.SoDienThoai);
            vm.Email = Gon(vm.Email);
            vm.NguoiDaiDien = Gon(vm.NguoiDaiDien);
            vm.GhiChuHoaDon = Gon(vm.GhiChuHoaDon);
            vm.DuongDanLogo = Gon(vm.DuongDanLogo);

            ModelState.Clear();
            if (string.IsNullOrWhiteSpace(vm.TenCuaHang))
                ModelState.AddModelError(nameof(vm.TenCuaHang), "Vui lòng nhập tên cửa hàng.");
            else if (vm.TenCuaHang.Length > 150)
                ModelState.AddModelError(nameof(vm.TenCuaHang), "Tên cửa hàng tối đa 150 ký tự.");
            if (vm.DiaChi != null && vm.DiaChi.Length > 255)
                ModelState.AddModelError(nameof(vm.DiaChi), "Địa chỉ tối đa 255 ký tự.");
            if (vm.SoDienThoai != null && (vm.SoDienThoai.Length > 20
                || vm.SoDienThoai.Any(c => !char.IsDigit(c) && c != ' ' && c != '.' && c != '-' && c != '+' && c != '(' && c != ')')))
                ModelState.AddModelError(nameof(vm.SoDienThoai), "Số điện thoại chỉ gồm chữ số và các ký tự + - . ( ).");
            if (vm.Email != null && (vm.Email.Length > 255 || !vm.Email.Contains('@') || vm.Email.StartsWith('@') || vm.Email.EndsWith('@')))
                ModelState.AddModelError(nameof(vm.Email), "Email không hợp lệ.");
            if (vm.NguoiDaiDien != null && vm.NguoiDaiDien.Length > 100)
                ModelState.AddModelError(nameof(vm.NguoiDaiDien), "Người đại diện tối đa 100 ký tự.");
            if (vm.DuongDanLogo != null && vm.DuongDanLogo.Length > 255)
                ModelState.AddModelError(nameof(vm.DuongDanLogo), "Đường dẫn logo tối đa 255 ký tự.");

            if (!ModelState.IsValid) return View(vm);

            var ch = await _context.CauHinhCuaHangs.FirstOrDefaultAsync(x => x.MaCauHinh == 1);
            if (ch == null)
            {
                vm.NgayCapNhat = DateTime.Now;
                _context.CauHinhCuaHangs.Add(vm);
            }
            else
            {
                ch.TenCuaHang = vm.TenCuaHang;
                ch.DiaChi = vm.DiaChi;
                ch.SoDienThoai = vm.SoDienThoai;
                ch.Email = vm.Email;
                ch.NguoiDaiDien = vm.NguoiDaiDien;
                ch.GhiChuHoaDon = vm.GhiChuHoaDon;
                ch.DuongDanLogo = vm.DuongDanLogo;
                ch.NgayCapNhat = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            TempData["ThongBao"] = "Đã lưu cấu hình cửa hàng.";
            return RedirectToAction(nameof(Edit));
        }

        // Đọc cấu hình; nếu bảng trống (DB cũ chưa chạy mục 29) thì trả bản mặc định
        // để màn hình sửa và trang in vẫn chạy được.
        private Task<CauHinhCuaHang> LayCauHinhAsync() => QuanLyKhoWeb.Services.CauHinhCuaHangService.LayAsync(_context);

        private static string? Gon(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
