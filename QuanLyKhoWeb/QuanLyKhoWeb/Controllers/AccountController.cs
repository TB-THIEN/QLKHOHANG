using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using QuanLyKhoWeb.Models.ViewModels;
using QuanLyKhoWeb.Services.Interfaces;

namespace QuanLyKhoWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly INguoiDungService _nguoiDungService;
        public AccountController(INguoiDungService nguoiDungService) => _nguoiDungService = nguoiDungService;

        [HttpGet, AllowAnonymous]
        public IActionResult DangNhap() => View();

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> DangNhap(DangNhapViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var nd = await _nguoiDungService.XacThucAsync(model.TenDangNhap, model.MatKhau);
            if (nd == null)
            {
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, nd.MaNguoiDung.ToString()),
                new Claim(ClaimTypes.Name, nd.HoTen),
                new Claim("VaiTro", nd.MaVaiTroNavigation.TenVaiTro),
                new Claim("CoQuyenHuyDon", nd.MaVaiTroNavigation.CoQuyenHuyDon.ToString()),
                new Claim("DuocNhanVanDon", nd.MaVaiTroNavigation.DuocNhanVanDon.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            // Nhân viên mới được cấp tài khoản phải đổi mật khẩu trước
            if (nd.PhaiDoiMatKhau)
                return RedirectToAction("DoiMatKhau", "NhanVien");
            return RedirectToAction("Index", "TongQuan");
        }

        [HttpPost]
        public async Task<IActionResult> DangXuat()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("DangNhap");
        }

        [AllowAnonymous]
        public IActionResult TuChoiTruyCap() => View();

        // Quên mật khẩu GỘP 1 TRANG: nhập email -> bấm Gửi mã (AJAX) -> nhập OTP + mật khẩu mới -> submit
        [HttpGet, AllowAnonymous]
        public IActionResult QuenMatKhau() => View(new KhoiPhucMatKhauVm());

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> QuenMatKhau(KhoiPhucMatKhauVm vm,
            [FromServices] QuanLyKhoWeb.Data.DoAnQlkContext db)
        {
            if (!ModelState.IsValid) return View(vm);

            var emailLower = vm.Email.Trim().ToLower();
            var nd = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                db.NguoiDungs, x => x.Email != null && x.Email.ToLower() == emailLower);
            if (nd == null)
            {
                ModelState.AddModelError(nameof(vm.Email), "Email này chưa gắn với tài khoản nào.");
                return View(vm);
            }

            if (vm.MaDatLai <= 0)
            {
                ModelState.AddModelError(nameof(vm.MaXacNhan), "Vui lòng bấm Gửi mã xác nhận trước.");
                return View(vm);
            }

            var banGhi = await db.MatKhauDatLais.FindAsync(vm.MaDatLai);
            // Mã phải còn hạn, chưa dùng, và đúng của email vừa nhập (chống tráo email sau khi gửi mã)
            if (banGhi == null || banGhi.DaSuDung || DateTime.Now > banGhi.NgayHetHan
                || banGhi.MaNguoiDung != nd.MaNguoiDung)
            {
                ModelState.AddModelError(nameof(vm.MaXacNhan), "Mã đã hết hạn. Vui lòng bấm Gửi mã để lấy mã mới.");
                return View(vm);
            }

            if (!BCrypt.Net.BCrypt.Verify(vm.MaXacNhan, banGhi.MaXacNhanHash))
            {
                ModelState.AddModelError(nameof(vm.MaXacNhan), "Mã xác nhận không đúng. Vui lòng thử lại.");
                return View(vm);
            }

            nd.MatKhauMaHoa = BCrypt.Net.BCrypt.HashPassword(vm.MatKhauMoi);
            nd.PhaiDoiMatKhau = false;
            nd.NgayCapNhat = DateTime.Now;
            banGhi.DaSuDung = true; // mã chỉ dùng được 1 lần
            await db.SaveChangesAsync();

            TempData["ThongBao"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại.";
            return RedirectToAction(nameof(DangNhap));
        }

        // Nút Gửi mã (AJAX): sinh OTP mới, vô hiệu hóa mã cũ, gửi mail thật, mã hết hạn sau 3 phút
        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuiMa(string email,
            [FromServices] QuanLyKhoWeb.Data.DoAnQlkContext db,
            [FromServices] QuanLyKhoWeb.Services.Interfaces.IEmailService emailService)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Json(new { thanhCong = false, thongBao = "Thiếu email nhận mã." });

            var emailLower = email.Trim().ToLower();
            var nd = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                db.NguoiDungs, x => x.Email != null && x.Email.ToLower() == emailLower);
            if (nd == null)
                return Json(new { thanhCong = false, thongBao = "Email này chưa gắn với tài khoản nào." });

            // Chống spam: mã vừa gửi chưa quá 30 giây thì chưa cho gửi lại
            var moiNhat = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                db.MatKhauDatLais.Where(m => m.MaNguoiDung == nd.MaNguoiDung && !m.DaSuDung)
                    .OrderByDescending(m => m.NgayTao));
            if (moiNhat != null && (DateTime.Now - moiNhat.NgayTao).TotalSeconds < 30)
            {
                var doi = 30 - (int)(DateTime.Now - moiNhat.NgayTao).TotalSeconds;
                return Json(new { thanhCong = false, thongBao = $"Mã vừa được gửi. Vui lòng đợi {doi} giây." });
            }

            // Vô hiệu hóa các mã cũ chưa dùng của tài khoản này
            var cu = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                db.MatKhauDatLais.Where(m => m.MaNguoiDung == nd.MaNguoiDung && !m.DaSuDung));
            foreach (var m in cu) m.DaSuDung = true;

            // Sinh OTP 6 số, chỉ lưu bản băm (không lưu plaintext), hết hạn sau 3 phút, dùng 1 lần
            var otp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
            var banGhi = new QuanLyKhoWeb.Models.Entities.MatKhauDatLai
            {
                MaNguoiDung = nd.MaNguoiDung,
                MaXacNhanHash = BCrypt.Net.BCrypt.HashPassword(otp),
                NgayTao = DateTime.Now,
                NgayHetHan = DateTime.Now.AddMinutes(3),
                DaSuDung = false
            };
            db.MatKhauDatLais.Add(banGhi);
            await db.SaveChangesAsync();

            try
            {
                await emailService.GuiMaXacNhanAsync(nd.Email!, otp);
            }
            catch (Exception ex)
            {
                return Json(new { thanhCong = false, thongBao = "Không gửi được email: " + ex.Message });
            }

            return Json(new { thanhCong = true, maDatLai = banGhi.MaDatLai, thongBao = $"Đã gửi mã về {nd.Email}." });
        }
    }
}
