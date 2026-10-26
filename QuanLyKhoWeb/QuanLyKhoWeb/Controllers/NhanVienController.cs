using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
    public class NhanVienController : Controller
    {
        private readonly DoAnQlkContext _context;
        private readonly IDbConnectionFactory _factory;
        private readonly IWebHostEnvironment _env;
        public NhanVienController(DoAnQlkContext context, IDbConnectionFactory factory, IWebHostEnvironment env)
        {
            _context = context;
            _factory = factory;
            _env = env;
        }

        // Danh sách nhân viên - chỉ Quản lý (policy ChiQuanLy).
        // Mặc định ẩn nhân viên nghỉ làm (không xóa); tick "Hiện NV nghỉ" để xem lại.
        [Authorize(Policy = "ChiQuanLy")]
        public async Task<IActionResult> Index(string? search, bool hienNghi = false)
        {
            ViewData["SearchValue"] = search;
            ViewBag.HienNghi = hienNghi;
            var q = _context.NguoiDungs.Include(n => n.MaVaiTroNavigation).AsQueryable();
            if (!hienNghi) q = q.Where(n => n.TrangThaiHoatDong == true);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(n => n.HoTen.Contains(search) || n.TenDangNhap.Contains(search));
            var list = await q.OrderBy(n => n.MaNguoiDung).ToListAsync();
            return View(list);
        }

        [Authorize(Policy = "ChiQuanLy")]
        public async Task<IActionResult> Details(int id)
        {
            var nv = await _context.NguoiDungs.Include(n => n.MaVaiTroNavigation)
                .FirstOrDefaultAsync(n => n.MaNguoiDung == id);
            if (nv == null) return NotFound();
            // Combobox Vai trò load qua API GoiYVaiTro nên không cần ViewBag preload
            // Đang xem chính mình thì view ẩn luôn thẻ Trạng thái & vai trò
            ViewBag.LaChinhMinh = id == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            // Lịch sử đổi thông tin (sp_LichSuThayDoiNhanVien): ai sửa, trường nào, cũ/mới
            try
            {
                using var connLichSu = (SqlConnection)_factory.CreateConnection();
                var pLichSu = new DynamicParameters();
                pLichSu.Add("@ma_nguoi_dung", id);
                await connLichSu.OpenAsync();
                ViewBag.LichSu = (await connLichSu.QueryAsync<dynamic>(
                    "sp_LichSuThayDoiNhanVien", pLichSu, commandType: CommandType.StoredProcedure)).ToList();
            }
            catch
            {
                ViewBag.LichSu = new List<dynamic>();
            }
            return View(nv);
        }

        // Quản lý đặt lại mật khẩu cho nhân viên (quên MK / khóa cũ lộ):
        // hash mới + bắt đổi ở lần đăng nhập kế tiếp, có ghi nhật ký người thực hiện.
        [HttpPost, Authorize(Policy = "ChiQuanLy"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DatLaiMatKhau(int id, string matKhauMoi)
        {
            if (string.IsNullOrWhiteSpace(matKhauMoi) || matKhauMoi.Length < 6)
            {
                TempData["Loi"] = "Mật khẩu mới phải tối thiểu 6 ký tự.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var maThucHien = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            try
            {
                using var conn = (SqlConnection)_factory.CreateConnection();
                var p = new DynamicParameters();
                p.Add("@ma_nguoi_dung", id);
                p.Add("@ma_nguoi_thuc_hien", maThucHien);
                p.Add("@mat_khau_ma_hoa_moi", BCrypt.Net.BCrypt.HashPassword(matKhauMoi));
                await conn.OpenAsync();
                await conn.ExecuteAsync("sp_DatLaiMatKhauNhanVien", p, commandType: CommandType.StoredProcedure);
                TempData["ThongBao"] = "Đã đặt lại mật khẩu. Nhân viên phải đổi ngay ở lần đăng nhập kế tiếp.";
            }
            catch (SqlException ex)
            {
                TempData["Loi"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // Quản lý sửa hồ sơ nhân viên (CCCD, ảnh thẻ, STK...) - qua sp_CapNhatNhanVien
        // để giữ nhật ký thay đổi + kiểm tra trùng email/CCCD (vai trò/trạng thái giữ
        // nguyên giá trị hiện tại; đổi vai trò/trạng thái vẫn làm ở DoiTrangThai)
        [HttpGet, Authorize(Policy = "ChiQuanLy")]
        public async Task<IActionResult> Edit(int id)
        {
            var nv = await _context.NguoiDungs.FindAsync(id);
            if (nv == null) return NotFound();
            return View(new NhanVienEditVm
            {
                MaNguoiDung = nv.MaNguoiDung,
                HoTen = nv.HoTen,
                SoDienThoai = nv.SoDienThoai,
                Email = nv.Email,
                ThuongTru = nv.ThuongTru,
                NgaySinh = nv.NgaySinh,
                GioiTinh = nv.GioiTinh,
                Cccd = nv.Cccd,
                AnhTheHienTai = nv.AnhThe,
                StkNganHang = nv.StkNganHang,
                ChiNhanhNganHang = nv.ChiNhanhNganHang
            });
        }

        [HttpPost, Authorize(Policy = "ChiQuanLy"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, NhanVienEditVm vm)
        {
            var nv = await _context.NguoiDungs.Include(n => n.MaVaiTroNavigation)
                .FirstOrDefaultAsync(n => n.MaNguoiDung == id);
            if (nv == null) return NotFound();

            if (!QuanLyKhoWeb.Services.KiemTraDauVao.LaTenHopLe(vm.HoTen))
                ModelState.AddModelError(nameof(vm.HoTen), "Họ tên không được chứa số.");
            string? duongDanAnh = null;
            if (vm.AnhTheMoi != null && vm.AnhTheMoi.Length > 0)
            {
                if (vm.AnhTheMoi.Length > 5 * 1024 * 1024)
                    ModelState.AddModelError(nameof(vm.AnhTheMoi), "Ảnh vượt quá 5MB.");
                else if (string.IsNullOrEmpty(vm.AnhTheMoi.ContentType) || !vm.AnhTheMoi.ContentType.StartsWith("image/"))
                    ModelState.AddModelError(nameof(vm.AnhTheMoi), "Vui lòng chọn đúng file hình ảnh.");
                else
                {
                    var thuMuc = Path.Combine(_env.WebRootPath, "images", "staff");
                    Directory.CreateDirectory(thuMuc);
                    var tenFile = Guid.NewGuid().ToString("N") + Path.GetExtension(vm.AnhTheMoi.FileName);
                    using (var stream = new FileStream(Path.Combine(thuMuc, tenFile), FileMode.Create))
                    {
                        await vm.AnhTheMoi.CopyToAsync(stream);
                    }
                    duongDanAnh = "/images/staff/" + tenFile;
                }
            }
            if (!ModelState.IsValid) return View(vm);

            var maThucHien = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            try
            {
                using var conn = (SqlConnection)_factory.CreateConnection();
                var p = new DynamicParameters();
                p.Add("@ma_nguoi_dung", id);
                p.Add("@ma_nguoi_thuc_hien", maThucHien);
                p.Add("@ten_dang_nhap", nv.TenDangNhap);
                p.Add("@ho_ten", vm.HoTen.Trim());
                p.Add("@vai_tro", nv.MaVaiTroNavigation.TenVaiTro);
                p.Add("@email", string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim());
                p.Add("@so_dien_thoai", string.IsNullOrWhiteSpace(vm.SoDienThoai) ? null : vm.SoDienThoai.Trim());
                p.Add("@ngay_sinh", vm.NgaySinh);
                p.Add("@gioi_tinh", string.IsNullOrWhiteSpace(vm.GioiTinh) ? null : vm.GioiTinh);
                p.Add("@thuong_tru", string.IsNullOrWhiteSpace(vm.ThuongTru) ? null : vm.ThuongTru.Trim());
                p.Add("@cccd", string.IsNullOrWhiteSpace(vm.Cccd) ? null : vm.Cccd.Trim());
                p.Add("@anh_the", duongDanAnh);
                p.Add("@stk_ngan_hang", string.IsNullOrWhiteSpace(vm.StkNganHang) ? null : vm.StkNganHang.Trim());
                p.Add("@chi_nhanh_ngan_hang", string.IsNullOrWhiteSpace(vm.ChiNhanhNganHang) ? null : vm.ChiNhanhNganHang.Trim());
                p.Add("@trang_thai_hoat_dong", nv.TrangThaiHoatDong ?? true);
                p.Add("@co_quyen_huy_don", false);
                p.Add("@duoc_nhan_van_don", true);
                await conn.OpenAsync();
                await conn.ExecuteAsync("sp_CapNhatNhanVien", p, commandType: CommandType.StoredProcedure);
                TempData["ThongBao"] = "Đã cập nhật hồ sơ nhân viên.";
            }
            catch (SqlException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
            return RedirectToAction(nameof(Details), new { id });
        }
        // được ghi vào nhat_ky_thay_doi_nguoi_dung (ai đổi, lúc nào) thay vì sửa EF trực tiếp.
        // Vai trò gõ mới sẽ được SP tự tạo (giống lúc Thêm mới); SP chặn có hơn 1 quản lý.
        [HttpPost, Authorize(Policy = "ChiQuanLy"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DoiTrangThai(int id, bool trangThai, string? tenVaiTro)
        {
            var nv = await _context.NguoiDungs.Include(n => n.MaVaiTroNavigation).FirstOrDefaultAsync(n => n.MaNguoiDung == id);
            if (nv == null) return NotFound();

            var maThucHien = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            // Không được tự sửa chính mình ở đây (tránh tự khóa đăng nhập / tự hạ quyền quản lý)
            if (id == maThucHien)
            {
                TempData["Loi"] = "Không thể tự thay đổi trạng thái hoặc vai trò của chính mình.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var vaiTroMoi = string.IsNullOrWhiteSpace(tenVaiTro) || tenVaiTro == "__MOI__"
                ? nv.MaVaiTroNavigation.TenVaiTro : tenVaiTro.Trim();
            try
            {
                using var conn = (SqlConnection)_factory.CreateConnection();
                var p = new DynamicParameters();
                p.Add("@ma_nguoi_dung", nv.MaNguoiDung);
                p.Add("@ma_nguoi_thuc_hien", maThucHien);
                p.Add("@ten_dang_nhap", nv.TenDangNhap);
                p.Add("@ho_ten", nv.HoTen);
                p.Add("@vai_tro", vaiTroMoi);
                p.Add("@email", nv.Email);
                p.Add("@so_dien_thoai", nv.SoDienThoai);
                p.Add("@ngay_sinh", nv.NgaySinh);
                p.Add("@gioi_tinh", nv.GioiTinh);
                p.Add("@thuong_tru", nv.ThuongTru);
                p.Add("@trang_thai_hoat_dong", trangThai);
                // Chỉ áp dụng khi vai trò MỚI hoàn toàn; vai trò đã có sẵn thì SP giữ nguyên quyền cũ.
                p.Add("@co_quyen_huy_don", false);
                p.Add("@duoc_nhan_van_don", true);
                await conn.OpenAsync();
                await conn.ExecuteAsync("sp_CapNhatNhanVien", p, commandType: CommandType.StoredProcedure);
                TempData["ThongBao"] = "Đã cập nhật trạng thái và vai trò nhân viên.";
            }
            catch (SqlException ex)
            {
                TempData["Loi"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Policy = "ChiQuanLy")]
        public IActionResult Create()
        {
            // Combobox Vai trò load qua API GoiYVaiTro nên không cần ViewBag preload
            return View(new NhanVienCreateVm());
        }

        // Thêm mới nhân viên - qua sp_TaoNhanVien để có đủ: kiểm tra trùng tên đăng nhập/email,
        // email chỉ nhận @gmail.com, tự tạo vai trò mới nếu chưa có, và chặn tạo quá 1 Quản lý.
        [HttpPost, Authorize(Policy = "ChiQuanLy"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NhanVienCreateVm vm)
        {
            // Combobox Vai trò load qua API GoiYVaiTro nên không cần ViewBag preload

            // Cảnh báo sớm cho UX; sp_TaoNhanVien vẫn kiểm tra lại lần cuối để tránh đụng độ.
            if (await _context.NguoiDungs.AnyAsync(n => n.TenDangNhap == vm.TenDangNhap))
                ModelState.AddModelError(nameof(vm.TenDangNhap), "Tên đăng nhập đã tồn tại, vui lòng chọn tên khác.");
            if (!QuanLyKhoWeb.Services.KiemTraDauVao.LaTenHopLe(vm.HoTen))
                ModelState.AddModelError(nameof(vm.HoTen), "Họ tên không được chứa số.");
            vm.TenVaiTro = (vm.TenVaiTro ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(vm.TenVaiTro))
                ModelState.AddModelError(nameof(vm.TenVaiTro), "Vui lòng chọn hoặc nhập vai trò.");
            var cccdMoi = string.IsNullOrWhiteSpace(vm.Cccd) ? null : vm.Cccd.Trim();
            if (cccdMoi != null && await _context.NguoiDungs.AnyAsync(n => n.Cccd == cccdMoi))
                ModelState.AddModelError(nameof(vm.Cccd), "CCCD này đã được sử dụng.");
            if (vm.AnhThe != null && vm.AnhThe.Length > 0)
            {
                if (vm.AnhThe.Length > 5 * 1024 * 1024)
                    ModelState.AddModelError(nameof(vm.AnhThe), "Ảnh vượt quá 5MB.");
                else if (string.IsNullOrEmpty(vm.AnhThe.ContentType) || !vm.AnhThe.ContentType.StartsWith("image/"))
                    ModelState.AddModelError(nameof(vm.AnhThe), "Vui lòng chọn đúng file hình ảnh.");
            }

            if (!ModelState.IsValid) return View(vm);

            // Lưu ảnh thẻ 3x4 vào wwwroot/images/staff
            string? duongDanAnh = null;
            if (vm.AnhThe != null && vm.AnhThe.Length > 0)
            {
                var thuMuc = Path.Combine(_env.WebRootPath, "images", "staff");
                Directory.CreateDirectory(thuMuc);
                var tenFile = Guid.NewGuid().ToString("N") + Path.GetExtension(vm.AnhThe.FileName);
                using (var stream = new FileStream(Path.Combine(thuMuc, tenFile), FileMode.Create))
                {
                    await vm.AnhThe.CopyToAsync(stream);
                }
                duongDanAnh = "/images/staff/" + tenFile;
            }

            try
            {
                using var conn = (SqlConnection)_factory.CreateConnection();
                var p = new DynamicParameters();
                p.Add("@ten_dang_nhap", vm.TenDangNhap);
                p.Add("@mat_khau_ma_hoa", BCrypt.Net.BCrypt.HashPassword(vm.MatKhau));
                p.Add("@ho_ten", vm.HoTen);
                p.Add("@vai_tro", vm.TenVaiTro);
                p.Add("@email", string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email);
                p.Add("@so_dien_thoai", vm.SoDienThoai);
                p.Add("@ngay_sinh", vm.NgaySinh);
                p.Add("@gioi_tinh", vm.GioiTinh);
                p.Add("@thuong_tru", vm.ThuongTru);
                p.Add("@cccd", cccdMoi);
                p.Add("@anh_the", duongDanAnh);
                p.Add("@stk_ngan_hang", string.IsNullOrWhiteSpace(vm.StkNganHang) ? null : vm.StkNganHang.Trim());
                p.Add("@chi_nhanh_ngan_hang", string.IsNullOrWhiteSpace(vm.ChiNhanhNganHang) ? null : vm.ChiNhanhNganHang.Trim());
                // Chỉ áp dụng khi vai trò CHƯA tồn tại (SP tự tạo vai trò mới với 2 quyền này);
                // nếu vai trò đã có sẵn thì SP giữ nguyên quyền cũ của vai trò đó.
                p.Add("@co_quyen_huy_don", false);
                p.Add("@duoc_nhan_van_don", true);
                await conn.OpenAsync();
                await conn.ExecuteAsync("sp_TaoNhanVien", p, commandType: CommandType.StoredProcedure);
            }
            catch (SqlException ex)
            {
                // Bắt các RAISERROR trong SP: trùng tên đăng nhập/email, email sai định dạng,
                // vai trò trống, hoặc "Không được có nhiều hơn 1 quản lý."
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }

            // sp_TaoNhanVien luôn tạo tài khoản ở trạng thái "đang làm"; nếu quản lý chọn sẵn
            // "Nghỉ" ngay lúc tạo thì cập nhật lại bằng EF (SP không nhận tham số này).
            if (!vm.TrangThaiHoatDong)
            {
                var nvMoi = await _context.NguoiDungs.FirstOrDefaultAsync(n => n.TenDangNhap == vm.TenDangNhap);
                if (nvMoi != null)
                {
                    nvMoi.TrangThaiHoatDong = false;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["ThongBao"] = $"Đã cấp tài khoản {vm.TenDangNhap} cho nhân viên.";
            return RedirectToAction(nameof(Index));
        }

        // API gợi ý vai trò cho combobox (click xổ list + gõ để lọc):
        // - term rỗng (click/focus): trả toàn bộ để hiện list 6 dòng + cuộn
        // - term có chữ: lọc Contains để gợi ý; frontend giới hạn hiển thị 6 dòng + cuộn
        // Ẩn vai trò Quản lý khỏi list (đã có đúng 1 quản lý là admin; SP cũng chặn tạo quản lý thứ 2)
        [Authorize(Policy = "ChiQuanLy")]
        public async Task<IActionResult> GoiYVaiTro(string? term)
        {
            var q = _context.VaiTros.Where(v => v.TenVaiTro != "Quản lý");
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();
                q = q.Where(v => v.TenVaiTro.Contains(term));
            }
            var list = await q.OrderBy(v => v.TenVaiTro).Select(v => v.TenVaiTro).Take(50).ToListAsync();
            return Json(list);
        }

        // API kiểm tra trùng tên đăng nhập (dùng cho cảnh báo realtime)
        [Authorize(Policy = "ChiQuanLy")]
        public async Task<IActionResult> KiemTraTrungTen(string tenDangNhap)
        {
            var trung = await _context.NguoiDungs.AnyAsync(n => n.TenDangNhap == tenDangNhap);
            return Json(new { trung });
        }

        // Thông tin cá nhân (Hình 4.1): Thường trú nhập tay, Vai trò không sửa, bỏ Khu vực/phường xã
        public async Task<IActionResult> ThongTinCaNhan()
        {
            var maNd = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var nv = await _context.NguoiDungs.Include(n => n.MaVaiTroNavigation)
                .FirstOrDefaultAsync(n => n.MaNguoiDung == maNd);
            if (nv == null) return NotFound();
            return View(new ThongTinCaNhanVm
            {
                MaNguoiDung = nv.MaNguoiDung,
                TenDangNhap = nv.TenDangNhap,
                TenVaiTro = nv.MaVaiTroNavigation.TenVaiTro,
                HoTen = nv.HoTen,
                SoDienThoai = nv.SoDienThoai,
                Email = nv.Email,
                ThuongTru = nv.ThuongTru,
                NgaySinh = nv.NgaySinh,
                GioiTinh = nv.GioiTinh,
                Cccd = nv.Cccd,
                AnhTheHienTai = nv.AnhThe,
                StkNganHang = nv.StkNganHang,
                ChiNhanhNganHang = nv.ChiNhanhNganHang
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ThongTinCaNhan(ThongTinCaNhanVm vm)
        {
            var maNd = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var nv = await _context.NguoiDungs.FindAsync(maNd);
            if (nv == null) return NotFound();
            if (!QuanLyKhoWeb.Services.KiemTraDauVao.LaTenHopLe(vm.HoTen))
                ModelState.AddModelError(nameof(vm.HoTen), "Họ tên không được chứa số.");
            var cccdMoi = string.IsNullOrWhiteSpace(vm.Cccd) ? null : vm.Cccd.Trim();
            if (vm.AnhTheMoi != null && vm.AnhTheMoi.Length > 0)
            {
                if (vm.AnhTheMoi.Length > 5 * 1024 * 1024)
                    ModelState.AddModelError(nameof(vm.AnhTheMoi), "Ảnh vượt quá 5MB.");
                else if (string.IsNullOrEmpty(vm.AnhTheMoi.ContentType) || !vm.AnhTheMoi.ContentType.StartsWith("image/"))
                    ModelState.AddModelError(nameof(vm.AnhTheMoi), "Vui lòng chọn đúng file hình ảnh.");
            }
            if (!ModelState.IsValid) return View(vm);

            // Email duy nhất toàn hệ thống (UX_nguoidung_email): báo lỗi thân thiện thay vì 500
            var emailMoi = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();
            if (emailMoi != null && await _context.NguoiDungs.AnyAsync(n => n.Email == emailMoi && n.MaNguoiDung != maNd))
            {
                ModelState.AddModelError(nameof(vm.Email), "Email này đã được tài khoản khác sử dụng.");
                return View(vm);
            }
            if (cccdMoi != null && await _context.NguoiDungs.AnyAsync(n => n.Cccd == cccdMoi && n.MaNguoiDung != maNd))
            {
                ModelState.AddModelError(nameof(vm.Cccd), "CCCD này đã được tài khoản khác sử dụng.");
                return View(vm);
            }

            if (vm.AnhTheMoi != null && vm.AnhTheMoi.Length > 0)
            {
                var thuMuc = Path.Combine(_env.WebRootPath, "images", "staff");
                Directory.CreateDirectory(thuMuc);
                var tenFile = Guid.NewGuid().ToString("N") + Path.GetExtension(vm.AnhTheMoi.FileName);
                using (var stream = new FileStream(Path.Combine(thuMuc, tenFile), FileMode.Create))
                {
                    await vm.AnhTheMoi.CopyToAsync(stream);
                }
                nv.AnhThe = "/images/staff/" + tenFile;
            }
            nv.HoTen = vm.HoTen;
            nv.SoDienThoai = vm.SoDienThoai;
            nv.Email = emailMoi;
            nv.ThuongTru = vm.ThuongTru;
            nv.NgaySinh = vm.NgaySinh;
            nv.GioiTinh = vm.GioiTinh;
            nv.Cccd = cccdMoi;
            nv.StkNganHang = string.IsNullOrWhiteSpace(vm.StkNganHang) ? null : vm.StkNganHang.Trim();
            nv.ChiNhanhNganHang = string.IsNullOrWhiteSpace(vm.ChiNhanhNganHang) ? null : vm.ChiNhanhNganHang.Trim();
            nv.NgayCapNhat = DateTime.Now;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Lưới an toàn khi 2 người cùng lưu email trùng một lúc
                ModelState.AddModelError(nameof(vm.Email), "Email này đã được tài khoản khác sử dụng.");
                return View(vm);
            }
            TempData["ThongBao"] = "Đã cập nhật thông tin cá nhân.";
            return RedirectToAction(nameof(ThongTinCaNhan));
        }

        // Trang đổi mật khẩu dùng chung cho mọi tài khoản:
        // - Lần đầu đăng nhập (PhaiDoiMatKhau = true): bắt buộc đổi, xong mới vào Tổng quan.
        // - Đổi tự nguyện từ trang Thông tin cá nhân: phải nhập đúng mật khẩu hiện tại.
        [HttpGet]
        public async Task<IActionResult> DoiMatKhau()
        {
            var maNd = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var nv = await _context.NguoiDungs.FindAsync(maNd);
            // Truyền cờ để view biết có phải đổi bắt buộc lần đầu không (ẩn nút Quay lại khi bắt buộc)
            ViewBag.BatBuocDoi = nv != null && nv.PhaiDoiMatKhau;
            return View(new DoiMatKhauVm());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DoiMatKhau(DoiMatKhauVm vm)
        {
            var maNd = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var nv = await _context.NguoiDungs.FindAsync(maNd);
            if (nv == null) return NotFound();

            ViewBag.BatBuocDoi = nv.PhaiDoiMatKhau;
            if (!ModelState.IsValid) return View(vm);

            // 1. Mật khẩu hiện tại phải đúng
            if (!BCrypt.Net.BCrypt.Verify(vm.MatKhauHienTai, nv.MatKhauMaHoa))
            {
                ModelState.AddModelError(nameof(vm.MatKhauHienTai), "Mật khẩu hiện tại không đúng.");
                return View(vm);
            }

            // 2. Mật khẩu mới phải khác mật khẩu hiện tại
            if (BCrypt.Net.BCrypt.Verify(vm.MatKhauMoi, nv.MatKhauMaHoa))
            {
                ModelState.AddModelError(nameof(vm.MatKhauMoi), "Mật khẩu mới phải khác mật khẩu hiện tại.");
                return View(vm);
            }

            nv.MatKhauMaHoa = BCrypt.Net.BCrypt.HashPassword(vm.MatKhauMoi);
            nv.PhaiDoiMatKhau = false;
            nv.NgayCapNhat = DateTime.Now;
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = "Đổi mật khẩu thành công.";

            // Đổi lần đầu -> vào Tổng quan; đổi tự nguyện -> quay về Thông tin cá nhân
            if (ViewBag.BatBuocDoi == true)
                return RedirectToAction("Index", "TongQuan");
            return RedirectToAction(nameof(ThongTinCaNhan));
        }
    }
}