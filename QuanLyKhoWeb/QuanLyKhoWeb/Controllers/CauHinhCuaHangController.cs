using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
        // Logo: 1 ảnh duy nhất, tối đa 5MB (giống giới hạn ảnh sản phẩm).
        private const long GioiHanDungLuongLogo = 5 * 1024 * 1024;

        private readonly DoAnQlkContext _context;
        private readonly IWebHostEnvironment _env;

        // Thư mục vật lý chứa logo (nằm trong wwwroot)
        private string ThuMucLogo => Path.Combine(ThuMucWeb, "images", "logo");

        // WebRootPath có thể null khi chạy exe từ thư mục bin (không có wwwroot).
        // Khi đó tìm ngược lên các thư mục cha để lấy wwwroot thật của project,
        // tránh tạo wwwroot rỗng trong bin (file ghi ra sẽ không được phục vụ).
        private string ThuMucWeb
        {
            get
            {
                if (!string.IsNullOrEmpty(_env.WebRootPath)) return _env.WebRootPath;
                var thuMuc = new DirectoryInfo(_env.ContentRootPath);
                while (thuMuc != null)
                {
                    var ungVien = Path.Combine(thuMuc.FullName, "wwwroot");
                    if (Directory.Exists(ungVien)) return ungVien;
                    thuMuc = thuMuc.Parent;
                }
                return Path.Combine(_env.ContentRootPath, "wwwroot");
            }
        }

        public CauHinhCuaHangController(DoAnQlkContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var ch = await LayCauHinhAsync();
            return View(ch);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CauHinhCuaHang vm, IFormFile? LogoFile, bool XoaLogo)
        {
            var hienTai = await LayCauHinhAsync();

            // Khóa chính luôn là 1, bỏ qua mọi giá trị gửi lên từ form
            vm.MaCauHinh = 1;
            vm.TenCuaHang = (vm.TenCuaHang ?? "").Trim();
            vm.DiaChi = Gon(vm.DiaChi);
            vm.SoDienThoai = Gon(vm.SoDienThoai);
            vm.Email = Gon(vm.Email);
            vm.NguoiDaiDien = Gon(vm.NguoiDaiDien);
            vm.GhiChuHoaDon = Gon(vm.GhiChuHoaDon);

            // Logo không nhập tay nữa: giữ đường dẫn đang lưu, chỉ đổi khi có file mới
            // hoặc người dùng bấm bỏ logo (xử lý sau khi dữ liệu hợp lệ).
            var logoCu = hienTai.DuongDanLogo;
            vm.DuongDanLogo = logoCu;
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

            // Kiểm tra file logo đã chọn (định dạng + dung lượng)
            KiemTraLogo(LogoFile);

            if (!ModelState.IsValid) return View(vm);

            // Ghi file logo mới ra đĩa (nếu có); lỗi lưu DB thì xóa lại cho khỏi rác.
            string? logoMoi = null;
            if (LogoFile != null && LogoFile.Length > 0)
            {
                logoMoi = await LuuLogoAsync(LogoFile);
                vm.DuongDanLogo = logoMoi;
            }
            else if (XoaLogo)
            {
                vm.DuongDanLogo = null;
            }

            try
            {
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
            }
            catch
            {
                if (logoMoi != null) XoaFileLogo(logoMoi);
                throw;
            }

            // Logo cũ đã bị thay/bỏ thì xóa file trên đĩa
            if (!string.IsNullOrEmpty(logoCu) && logoCu != vm.DuongDanLogo)
                XoaFileLogo(logoCu);

            TempData["ThongBao"] = "Đã lưu cấu hình cửa hàng.";
            return RedirectToAction(nameof(Edit));
        }

        // ===== Logo cửa hàng: 1 ảnh duy nhất, chọn từ máy như ảnh sản phẩm =====

        // Kiểm tra file logo: phải là ảnh, không quá 5MB.
        private void KiemTraLogo(IFormFile? file)
        {
            if (file == null || file.Length <= 0) return;
            if (file.Length > GioiHanDungLuongLogo)
                ModelState.AddModelError("LogoFile", "Ảnh logo vượt quá 5MB, vui lòng chọn ảnh nhỏ hơn.");
            else if (string.IsNullOrEmpty(file.ContentType) || !file.ContentType.StartsWith("image/"))
                ModelState.AddModelError("LogoFile", $"File \"{file.FileName}\" không phải là hình ảnh.");
        }

        // Ghi file logo lên đĩa với tên ngẫu nhiên, trả về đường dẫn web (/images/logo/...).
        private async Task<string> LuuLogoAsync(IFormFile file)
        {
            Directory.CreateDirectory(ThuMucLogo);
            var tenFile = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
            using (var stream = new FileStream(Path.Combine(ThuMucLogo, tenFile), FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return "/images/logo/" + tenFile;
        }

        // Xóa file logo vật lý (chỉ trong thư mục logo để an toàn).
        private void XoaFileLogo(string? duongDan)
        {
            if (string.IsNullOrEmpty(duongDan) || !duongDan.StartsWith("/images/logo/")) return;
            try
            {
                var duongDanDay = Path.Combine(ThuMucWeb, duongDan.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(duongDanDay)) System.IO.File.Delete(duongDanDay);
            }
            catch { /* xóa file thất bại thì bỏ qua */ }
        }

        // Đọc cấu hình; nếu bảng trống (DB cũ chưa chạy mục 29) thì trả bản mặc định
        // để màn hình sửa và trang in vẫn chạy được.
        private Task<CauHinhCuaHang> LayCauHinhAsync() => QuanLyKhoWeb.Services.CauHinhCuaHangService.LayAsync(_context);

        private static string? Gon(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
