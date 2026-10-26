using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Models.ViewModels;
using QuanLyKhoWeb.Services.Interfaces;
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Security.Claims;
using System.Text;

namespace QuanLyKhoWeb.Controllers
{
    public class SanPhamController : Controller
    {
        private readonly DoAnQlkContext _context;
        private readonly IDbConnectionFactory _factory;
        private readonly IWebHostEnvironment _env;

        // Giới hạn dung lượng ảnh sản phẩm tải lên (5MB) - đủ dùng, tránh chiếm dung lượng ổ đĩa.
        private const long GioiHanDungLuongAnh = 5 * 1024 * 1024;

        public SanPhamController(DoAnQlkContext context, IDbConnectionFactory factory, IWebHostEnvironment env)
        {
            _context = context;
            _factory = factory;
            _env = env;
        }

        // Danh sách: Mã SKU, hình ảnh, tên, giá, lô hàng, chi tiết (eye)
        // + tìm kiếm text + bộ lọc nhiều thuộc tính (danh mục, nhãn hiệu, loại,
        // chất liệu, màu sắc, lô hàng, tình trạng tồn)
        public async Task<IActionResult> Index(string? search, string? loHang,
            string? danhMuc, string? nhanHieu, string? loaiSanPham,
            string? chatLieu, string? mauSac, string? tonKho, int trang = 1)
        {
            ViewData["SearchValue"] = search;
            ViewBag.LoHang = loHang;
            ViewBag.DanhMuc = danhMuc;
            ViewBag.NhanHieu = nhanHieu;
            ViewBag.LoaiSanPham = loaiSanPham;
            ViewBag.ChatLieu = chatLieu;
            ViewBag.MauSac = mauSac;
            ViewBag.TonKho = tonKho;

            // Nguồn cho các dropdown lọc (chỉ lấy giá trị đang có dữ liệu)
            ViewBag.DsDanhMuc = await _context.DanhMucs.OrderBy(d => d.TenDanhMuc).Select(d => d.TenDanhMuc).ToListAsync();
            ViewBag.DsNhanHieu = await _context.MauSanPhams.Where(m => m.NhanHieu != null && m.NhanHieu != "").Select(m => m.NhanHieu!).Distinct().OrderBy(x => x).ToListAsync();
            ViewBag.DsLoai = await _context.MauSanPhams.Where(m => m.LoaiSanPham != null && m.LoaiSanPham != "").Select(m => m.LoaiSanPham!).Distinct().OrderBy(x => x).ToListAsync();
            ViewBag.DsChatLieu = await _context.MauSanPhams.Where(m => m.ChatLieu != null && m.ChatLieu != "").Select(m => m.ChatLieu!).Distinct().OrderBy(x => x).ToListAsync();
            ViewBag.DsMauSac = await _context.MauSanPhams.Where(m => m.MauSac != null && m.MauSac != "").Select(m => m.MauSac!).Distinct().OrderBy(x => x).ToListAsync();

            var q = _context.SanPhamSkus
                .Include(s => s.MaMauSanPhamNavigation).ThenInclude(m => m.MaDanhMucNavigation)
                .Include(s => s.MaLoHangNavigation)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(s => s.MaCodeSku.Contains(search) || s.MaMauSanPhamNavigation.TenSanPham.Contains(search));
            if (!string.IsNullOrWhiteSpace(loHang))
                q = q.Where(s => s.MaLoHangNavigation != null && s.MaLoHangNavigation.MaCodeLoHang.Contains(loHang));
            if (!string.IsNullOrWhiteSpace(danhMuc))
                q = q.Where(s => s.MaMauSanPhamNavigation.MaDanhMucNavigation.TenDanhMuc == danhMuc);
            if (!string.IsNullOrWhiteSpace(nhanHieu))
                q = q.Where(s => s.MaMauSanPhamNavigation.NhanHieu == nhanHieu);
            if (!string.IsNullOrWhiteSpace(loaiSanPham))
                q = q.Where(s => s.MaMauSanPhamNavigation.LoaiSanPham == loaiSanPham);
            if (!string.IsNullOrWhiteSpace(chatLieu))
                q = q.Where(s => s.MaMauSanPhamNavigation.ChatLieu == chatLieu);
            if (!string.IsNullOrWhiteSpace(mauSac))
                q = q.Where(s => s.MaMauSanPhamNavigation.MauSac == mauSac);
            if (tonKho == "conhang")
                q = q.Where(s => s.SoLuong > 5);
            else if (tonKho == "saphet")
                q = q.Where(s => s.SoLuong > 0 && s.SoLuong <= 5);
            else if (tonKho == "hethang")
                q = q.Where(s => s.SoLuong == 0);
            // Phân trang 48 sp/trang (giữ bộ lọc khi chuyển trang ở view)
            const int pageSize = 48;
            var total = await q.CountAsync();
            if (trang < 1) trang = 1;
            var totalPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            if (trang > totalPage) trang = totalPage;
            ViewBag.Trang = trang;
            ViewBag.TotalPage = totalPage;
            ViewBag.Total = total;
            var list = await q.OrderByDescending(s => s.MaSku).Skip((trang - 1) * pageSize).Take(pageSize).ToListAsync();
            return View(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var s = await _context.SanPhamSkus
                .Include(x => x.MaMauSanPhamNavigation).ThenInclude(m => m.MaDanhMucNavigation)
                .Include(x => x.MaLoHangNavigation)
                .FirstOrDefaultAsync(x => x.MaSku == id);
            if (s == null) return NotFound();
            return View(s);
        }

        // Tạo/sửa sản phẩm: Quản lý + NV kho + NV bán hàng (chặn NV giao hàng).
        // Xóa hẳn vẫn chỉ Quản lý (policy riêng ở action Xoa).
        [HttpGet, Authorize(Policy = "DuocTao")]
        public async Task<IActionResult> Create()
        {
            ViewBag.DanhMucs = await _context.DanhMucs.Select(d => d.TenDanhMuc).ToListAsync();
            ViewBag.Khus = await _context.KhuVucKhos.OrderBy(k => k.TenKhuVuc).ToListAsync();
            return View(new SanPhamCreateVm());
        }

        // Kệ thuộc 1 khu (để chọn vị trí xếp ngay khi thêm sản phẩm)
        public async Task<IActionResult> KeTheoKhu(int maKhuVuc)
        {
            var list = await _context.KeHangs
                .Where(k => k.MaKhuVuc == maKhuVuc)
                .OrderBy(k => k.TenKeHang)
                .Select(k => new { k.MaKeHang, k.TenKeHang }).ToListAsync();
            return Json(list);
        }

        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SanPhamCreateVm vm)
        {
            ViewBag.DanhMucs = await _context.DanhMucs.Select(d => d.TenDanhMuc).ToListAsync();
            ViewBag.Khus = await _context.KhuVucKhos.OrderBy(k => k.TenKhuVuc).ToListAsync();

            // Kiểm tra file ảnh (nếu có chọn): đúng định dạng ảnh + không vượt giới hạn dung lượng.
            if (vm.HinhAnh != null && vm.HinhAnh.Length > 0)
            {
                if (vm.HinhAnh.Length > GioiHanDungLuongAnh)
                    ModelState.AddModelError(nameof(vm.HinhAnh), "Ảnh vượt quá 5MB, vui lòng chọn ảnh nhỏ hơn.");
                else if (string.IsNullOrEmpty(vm.HinhAnh.ContentType) || !vm.HinhAnh.ContentType.StartsWith("image/"))
                    ModelState.AddModelError(nameof(vm.HinhAnh), "Vui lòng chọn đúng file hình ảnh.");
            }

            // Tên sản phẩm không được chứa số; các ô số nhập chữ/số thập phân thì báo lại rõ ràng
            if (!QuanLyKhoWeb.Services.KiemTraDauVao.LaTenHopLe(vm.TenSanPham))
                ModelState.AddModelError(nameof(vm.TenSanPham), "Tên sản phẩm không được chứa số.");
            QuanLyKhoWeb.Services.KiemTraDauVao.BaoLoiSoNguyen(ModelState, nameof(vm.GiaNiemYet),
                "Giá tiền phải là số lớn hơn hoặc bằng 0 (không nhập chữ).",
                nameof(vm.GiaNiemYet));

            var soLuongTrenKe = vm.SoLuongTrenKe ?? 0;
            QuanLyKhoWeb.Services.KiemTraDauVao.BaoLoiSoNguyen(ModelState, nameof(vm.SoLuongTrenKe),
                "Số lượng trên kệ phải là số nguyên lớn hơn hoặc bằng 0 (không nhập chữ hay số thập phân).",
                nameof(vm.SoLuongTrenKe));

            if (vm.MaKhuVuc.HasValue && !await _context.KhuVucKhos.AnyAsync(k => k.MaKhuVuc == vm.MaKhuVuc.Value))
                ModelState.AddModelError(nameof(vm.MaKhuVuc), "Khu hàng không hợp lệ.");
            if (vm.MaKeHang.HasValue && !vm.MaKhuVuc.HasValue)
                ModelState.AddModelError(nameof(vm.MaKeHang), "Vui lòng chọn khu hàng trước khi chọn kệ.");
            else if (vm.MaKeHang.HasValue && !await _context.KeHangs.AnyAsync(k => k.MaKeHang == vm.MaKeHang.Value && k.MaKhuVuc == vm.MaKhuVuc))
                ModelState.AddModelError(nameof(vm.MaKeHang), "Kệ hàng không thuộc khu đã chọn.");
            if (soLuongTrenKe > 0 && !vm.MaKhuVuc.HasValue)
                ModelState.AddModelError(nameof(vm.MaKhuVuc), "Vui lòng chọn khu hàng khi nhập số lượng trên kệ.");
            if (vm.MaKeHang.HasValue && soLuongTrenKe <= 0)
                ModelState.AddModelError(nameof(vm.SoLuongTrenKe), "Vui lòng nhập số lượng lớn hơn 0 khi chọn kệ.");

            if (!ModelState.IsValid) return View(vm);

            // Lưu ảnh vào wwwroot/images/products với tên ngẫu nhiên (tránh trùng/đè file, tránh
            // lộ tên file gốc từ máy nhân viên). Không chọn ảnh thì để trống như trước đây.
            string? duongDanHinhAnh = null;
            if (vm.HinhAnh != null && vm.HinhAnh.Length > 0)
            {
                var thuMuc = Path.Combine(_env.WebRootPath, "images", "products");
                Directory.CreateDirectory(thuMuc);
                var tenFile = Guid.NewGuid().ToString("N") + Path.GetExtension(vm.HinhAnh.FileName);
                var duongDanDay = Path.Combine(thuMuc, tenFile);
                using (var stream = new FileStream(duongDanDay, FileMode.Create))
                {
                    await vm.HinhAnh.CopyToAsync(stream);
                }
                duongDanHinhAnh = "/images/products/" + tenFile;
            }

            var maNguoi = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
            try
            {
                using var conn = (SqlConnection)_factory.CreateConnection();
                var p = new DynamicParameters();
                p.Add("@ten_san_pham", vm.TenSanPham);
                p.Add("@gia_niem_yet", vm.GiaNiemYet!.Value);
                p.Add("@so_luong", soLuongTrenKe);
                p.Add("@danh_muc", vm.DanhMuc);
                p.Add("@nhan_hieu", vm.NhanHieu);
                p.Add("@loai_san_pham", vm.LoaiSanPham);
                p.Add("@chat_lieu", vm.ChatLieu);
                p.Add("@mau_sac", vm.MauSac);
                p.Add("@mo_ta", vm.MoTa);
                p.Add("@duong_dan_hinh_anh", duongDanHinhAnh);
                p.Add("@ngay_lo_hang", vm.NgayLoHang!.Value.Date);
                p.Add("@ma_nguoi_thuc_hien", maNguoi);
                await conn.OpenAsync();
                // sp_TaoSanPham tự sinh mã SKU dạng NHOM-TEN-THUOCTINH-YYMM###, tự tạo danh mục text mới
                var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "sp_TaoSanPham", p, commandType: CommandType.StoredProcedure);
                string maSku = row?.ma_code_sku ?? "";
                int maSkuMoi = row?.ma_sku ?? 0;

                if (soLuongTrenKe > 0 && maSkuMoi > 0)
                {
                    try
                    {
                        if (vm.MaKeHang.HasValue)
                        {
                            var pk = new DynamicParameters();
                            pk.Add("@ma_sku", maSkuMoi);
                            pk.Add("@ma_ke_hang", vm.MaKeHang.Value);
                            pk.Add("@so_luong", soLuongTrenKe);
                            pk.Add("@ma_nguoi_thuc_hien", maNguoi);
                            await conn.ExecuteAsync("sp_ThemSkuVaoKe", pk, commandType: CommandType.StoredProcedure);
                            TempData["ThongBao"] = $"Đã tạo sản phẩm, mã SKU {maSku}, xếp {soLuongTrenKe} lên kệ.";
                        }
                        else if (vm.MaKhuVuc.HasValue)
                        {
                            var pk = new DynamicParameters();
                            pk.Add("@ma_sku", maSkuMoi);
                            pk.Add("@ma_khu_vuc", vm.MaKhuVuc.Value);
                            pk.Add("@so_luong", soLuongTrenKe);
                            pk.Add("@ma_nguoi_thuc_hien", maNguoi);
                            await conn.ExecuteAsync("sp_ThemSkuVaoKhu", pk, commandType: CommandType.StoredProcedure);
                            TempData["ThongBao"] = $"Đã tạo sản phẩm, mã SKU {maSku}, xếp {soLuongTrenKe} vào khu.";
                        }
                    }
                    catch (SqlException ex)
                    {
                        TempData["Loi"] = $"Đã tạo sản phẩm {maSku} nhưng chưa xếp được vào vị trí: {ex.Message}";
                    }
                }
                else
                {
                    TempData["ThongBao"] = $"Đã tạo sản phẩm, mã SKU {maSku}. Tạo phiếu nhập kho để cộng tồn.";
                }
                return RedirectToAction(nameof(Index));
            }
            catch (SqlException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }

        // Sửa thông tin sản phẩm: đổi tên, phân loại, giá, tồn, ảnh...
        // Mã SKU và lô hàng chốt từ lúc tạo nên chỉ hiển thị, không cho sửa.
            // Đổi số lượng tay được ghi vào nhật ký tồn kho (DIEU_CHINH_TAY) để còn vết truy xuất.
        [HttpGet, Authorize(Policy = "DuocTao")]
        public async Task<IActionResult> Edit(int id)
        {
            var s = await _context.SanPhamSkus
                .Include(x => x.MaMauSanPhamNavigation).ThenInclude(m => m.MaDanhMucNavigation)
                .Include(x => x.MaLoHangNavigation)
                .FirstOrDefaultAsync(x => x.MaSku == id);
            if (s == null) return NotFound();
            var m = s.MaMauSanPhamNavigation;
            ViewBag.DanhMucs = await _context.DanhMucs.Select(d => d.TenDanhMuc).ToListAsync();
            return View(new SanPhamEditVm
            {
                MaSku = s.MaSku,
                MaCodeSku = s.MaCodeSku,
                LoHang = s.MaLoHangNavigation?.MaCodeLoHang,
                TenSanPham = m.TenSanPham,
                DanhMuc = m.MaDanhMucNavigation?.TenDanhMuc,
                NhanHieu = m.NhanHieu,
                LoaiSanPham = m.LoaiSanPham,
                ChatLieu = m.ChatLieu,
                MauSac = m.MauSac,
                GiaNiemYet = s.GiaNiemYet,
                SoLuong = s.SoLuong,
                MoTa = m.MoTa,
                HinhAnhHienTai = m.DuongDanHinhAnh
            });
        }

        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SanPhamEditVm vm)
        {
            var s = await _context.SanPhamSkus
                .Include(x => x.MaMauSanPhamNavigation).ThenInclude(m => m.MaDanhMucNavigation)
                .FirstOrDefaultAsync(x => x.MaSku == id);
            if (s == null) return NotFound();
            ViewBag.DanhMucs = await _context.DanhMucs.Select(d => d.TenDanhMuc).ToListAsync();

            if (vm.HinhAnhMoi != null && vm.HinhAnhMoi.Length > 0)
            {
                if (vm.HinhAnhMoi.Length > GioiHanDungLuongAnh)
                    ModelState.AddModelError(nameof(vm.HinhAnhMoi), "Ảnh vượt quá 5MB, vui lòng chọn ảnh nhỏ hơn.");
                else if (string.IsNullOrEmpty(vm.HinhAnhMoi.ContentType) || !vm.HinhAnhMoi.ContentType.StartsWith("image/"))
                    ModelState.AddModelError(nameof(vm.HinhAnhMoi), "Vui lòng chọn đúng file hình ảnh.");
            }

            if (!QuanLyKhoWeb.Services.KiemTraDauVao.LaTenHopLe(vm.TenSanPham))
                ModelState.AddModelError(nameof(vm.TenSanPham), "Tên sản phẩm không được chứa số.");
            QuanLyKhoWeb.Services.KiemTraDauVao.BaoLoiSoNguyen(ModelState, nameof(vm.SoLuong),
                "Số lượng phải là số nguyên lớn hơn hoặc bằng 0 (không nhập chữ hay số thập phân).",
                nameof(vm.SoLuong));

            if (!ModelState.IsValid) return View(vm);

            var m = s.MaMauSanPhamNavigation;
            var anhCu = m.DuongDanHinhAnh;
            if (vm.HinhAnhMoi != null && vm.HinhAnhMoi.Length > 0)
            {
                var thuMuc = Path.Combine(_env.WebRootPath, "images", "products");
                Directory.CreateDirectory(thuMuc);
                var tenFile = Guid.NewGuid().ToString("N") + Path.GetExtension(vm.HinhAnhMoi.FileName);
                using (var stream = new FileStream(Path.Combine(thuMuc, tenFile), FileMode.Create))
                {
                    await vm.HinhAnhMoi.CopyToAsync(stream);
                }
                m.DuongDanHinhAnh = "/images/products/" + tenFile;
            }

            // Danh mục gõ mới thì tự tạo (sinh mã nhóm như sp_TaoSanPham)
            // Bọc transaction: lỗi ở bước sau thì rollback cả danh mục vừa tạo, khỏi mồ côi
            await using var txEdit = await _context.Database.BeginTransactionAsync();
            try
            {
                var tenDm = string.IsNullOrWhiteSpace(vm.DanhMuc) ? "Chưa phân loại" : vm.DanhMuc.Trim();
                var dm = await _context.DanhMucs.FirstOrDefaultAsync(d => d.TenDanhMuc == tenDm);
                if (dm == null)
                {
                    var maGoc = new string(BoDau(tenDm).Where(char.IsLetterOrDigit).ToArray()).ToUpper();
                    maGoc = maGoc.Length <= 3 ? maGoc : maGoc.Substring(0, 3);
                    if (maGoc == "") maGoc = "DM";
                    var maThu = maGoc; var dem = 1;
                    while (await _context.DanhMucs.AnyAsync(d => d.MaCodeDanhMuc == maThu))
                    {
                        dem++;
                        maThu = maGoc + dem;
                    }
                    dm = new QuanLyKhoWeb.Models.Entities.DanhMuc { MaCodeDanhMuc = maThu, TenDanhMuc = tenDm };
                    _context.DanhMucs.Add(dm);
                    await _context.SaveChangesAsync();
                }

                m.TenSanPham = vm.TenSanPham.Trim();
                m.MaDanhMuc = dm.MaDanhMuc;
                m.NhanHieu = string.IsNullOrWhiteSpace(vm.NhanHieu) ? null : vm.NhanHieu.Trim();
                m.LoaiSanPham = string.IsNullOrWhiteSpace(vm.LoaiSanPham) ? null : vm.LoaiSanPham.Trim();
                m.ChatLieu = string.IsNullOrWhiteSpace(vm.ChatLieu) ? null : vm.ChatLieu.Trim();
                m.MauSac = string.IsNullOrWhiteSpace(vm.MauSac) ? null : vm.MauSac.Trim();
                m.MoTa = string.IsNullOrWhiteSpace(vm.MoTa) ? null : vm.MoTa.Trim();
                s.GiaNiemYet = vm.GiaNiemYet;

                // Đổi tồn tay: giữ vết trong nhật ký để đối chiếu sau này
                var chenhLech = vm.SoLuong - s.SoLuong;
                if (chenhLech != 0)
                {
                    var maNguoi = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;
                    s.SoLuong = vm.SoLuong;
                    _context.NhatKyTonKhos.Add(new QuanLyKhoWeb.Models.Entities.NhatKyTonKho
                    {
                        MaSku = s.MaSku,
                        LoaiBienDong = "DIEU_CHINH_TAY",
                        SoLuongThayDoi = chenhLech,
                        MaNguoiThucHien = maNguoi
                    });
                }
                await _context.SaveChangesAsync();
                await txEdit.CommitAsync();
            }
            catch (Exception ex)
            {
                await txEdit.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Lưu thất bại: " + ex.Message);
                return View(vm);
            }

            // Dọn ảnh cũ sau khi đã lưu xong (tránh mất ảnh nếu lưu lỗi)
            if (vm.HinhAnhMoi != null && vm.HinhAnhMoi.Length > 0
                && !string.IsNullOrEmpty(anhCu) && anhCu.StartsWith("/images/products/"))
            {
                try
                {
                    var duongDanCu = Path.Combine(_env.WebRootPath, anhCu.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(duongDanCu)) System.IO.File.Delete(duongDanCu);
                }
                catch { /* xóa file thất bại thì bỏ qua */ }
            }

            TempData["ThongBao"] = $"Đã cập nhật sản phẩm {s.MaCodeSku}.";
            return RedirectToAction(nameof(Details), new { id = s.MaSku });
        }

        // Bỏ dấu tiếng Việt để sinh mã nhóm danh mục (đ/Đ xử lý riêng)
        private static string BoDau(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s.Normalize(NormalizationForm.FormD))
            {
                var loai = CharUnicodeInfo.GetUnicodeCategory(c);
                if (loai != UnicodeCategory.NonSpacingMark) sb.Append(c);
            }
            return sb.ToString().Replace('đ', 'd').Replace('Đ', 'D');
        }

        // Xóa sản phẩm: Quản lý + Nhân viên kho + Nhân viên bán hàng (chặn NV giao hàng).
        // CHẶN khi sản phẩm đã có trong đơn hàng (giữ lịch sử bán hàng); các trường hợp
        // còn lại xóa dây chuyền (dòng phiếu nhập, vị trí kệ, phân bổ xuất kho,
        // nhật ký tồn, liên kết khuyến mãi), dọn mẫu SP (nếu hết SKU dùng) và file ảnh.
        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Xoa(int id)
        {
            var s = await _context.SanPhamSkus
                .Include(x => x.MaMauSanPhamNavigation)
                .FirstOrDefaultAsync(x => x.MaSku == id);
            if (s == null) return NotFound();

            // Đã phát sinh đơn hàng thì không cho xóa dưới mọi hình thức
            if (await _context.ChiTietDonHangs.AnyAsync(c => c.MaSku == id))
            {
                TempData["Loi"] = $"Không thể xóa {s.MaCodeSku} vì sản phẩm đã có trong đơn hàng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var mau = s.MaMauSanPhamNavigation;
            var hinh = mau?.DuongDanHinhAnh;
            var maCode = s.MaCodeSku;
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Xóa dòng phiếu nhập (trigger tự tính lại tổng phiếu)
                _context.ChiTietPhieuNhapKhos.RemoveRange(_context.ChiTietPhieuNhapKhos.Where(c => c.MaSku == id));

                // 2. Xóa vị trí kệ, phân bổ xuất kho, nhật ký tồn kho
                _context.TonKhoKeHangs.RemoveRange(_context.TonKhoKeHangs.Where(t => t.MaSku == id));
                _context.PhanBoXuatKhoTheoKes.RemoveRange(_context.PhanBoXuatKhoTheoKes.Where(p => p.MaSku == id));
                _context.NhatKyTonKhos.RemoveRange(_context.NhatKyTonKhos.Where(n => n.MaSku == id));

                // 3. Xóa SKU + dọn mẫu SP nếu hết SKU dùng
                _context.SanPhamSkus.Remove(s);
                if (mau != null && !await _context.SanPhamSkus.AnyAsync(x => x.MaSku != id && x.MaMauSanPham == mau.MaMauSanPham))
                    _context.MauSanPhams.Remove(mau);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                // Dọn file ảnh đã tải lên (chỉ trong thư mục products để an toàn)
                if (!string.IsNullOrEmpty(hinh) && hinh.StartsWith("/images/products/"))
                {
                    try
                    {
                        var duongDan = Path.Combine(_env.WebRootPath, hinh.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(duongDan)) System.IO.File.Delete(duongDan);
                    }
                    catch { /* xóa file thất bại thì bỏ qua, dữ liệu đã xóa xong */ }
                }
                TempData["ThongBao"] = $"Đã xóa sản phẩm {maCode} cùng dữ liệu liên quan (kệ hàng, phiếu nhập, nhật ký tồn).";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                TempData["Loi"] = $"Không thể xóa {maCode}: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // Gợi ý autocomplete cho 3 ô nhập tay: danh mục / nhãn hiệu / loại sản phẩm
        public async Task<IActionResult> GoiY(string loai, string term)
        {
            term = (term ?? "").Trim();
            List<string> list;
            if (loai == "nhanhieu")
                list = await _context.MauSanPhams
                    .Where(m => m.NhanHieu != null && m.NhanHieu != "" && (term == "" || m.NhanHieu.Contains(term)))
                    .Select(m => m.NhanHieu!).Distinct().Take(10).ToListAsync();
            else if (loai == "loai")
                list = await _context.MauSanPhams
                    .Where(m => m.LoaiSanPham != null && m.LoaiSanPham != "" && (term == "" || m.LoaiSanPham.Contains(term)))
                    .Select(m => m.LoaiSanPham!).Distinct().Take(10).ToListAsync();
            else if (loai == "chatlieu")
                list = await _context.MauSanPhams
                    .Where(m => m.ChatLieu != null && m.ChatLieu != "" && (term == "" || m.ChatLieu.Contains(term)))
                    .Select(m => m.ChatLieu!).Distinct().Take(10).ToListAsync();
            else if (loai == "mausac")
                list = await _context.MauSanPhams
                    .Where(m => m.MauSac != null && m.MauSac != "" && (term == "" || m.MauSac.Contains(term)))
                    .Select(m => m.MauSac!).Distinct().Take(10).ToListAsync();
            else
                // Ẩn các danh mục placeholder "chưa phân loại" (có/không dấu): SP sp_TaoSanPham
                // tự tạo lại khi để trống danh mục, nhưng không cho hiện lên combobox để khỏi rối.
                list = await _context.DanhMucs
                    .Where(d => !d.TenDanhMuc.Contains("phân loại") && !d.TenDanhMuc.Contains("phan loai"))
                    .Where(d => term == "" || d.TenDanhMuc.Contains(term))
                    .OrderBy(d => d.TenDanhMuc)
                    .Select(d => d.TenDanhMuc).Take(10).ToListAsync();
            return Json(list);
        }
    }
}