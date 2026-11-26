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

        // Số ảnh tối đa cho mỗi sản phẩm (ảnh đầu tiên là ảnh đại diện).
        private const int SoAnhToiDa = 8;

        // Thư mục vật lý chứa ảnh sản phẩm (nằm trong wwwroot)
        private string ThuMucAnh => Path.Combine(_env.WebRootPath, "images", "products");

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
                q = q.Where(s => s.MaCodeSku.Contains(search)
                    || s.MaMauSanPhamNavigation.TenSanPham.Contains(search)
                    || (s.MaMauSanPhamNavigation.NhanHieu != null && s.MaMauSanPhamNavigation.NhanHieu.Contains(search))
                    || (s.MaMauSanPhamNavigation.ChatLieu != null && s.MaMauSanPhamNavigation.ChatLieu.Contains(search))
                    || (s.MaMauSanPhamNavigation.MauSac != null && s.MaMauSanPhamNavigation.MauSac.Contains(search))
                    || (s.MaMauSanPhamNavigation.LoaiSanPham != null && s.MaMauSanPhamNavigation.LoaiSanPham.Contains(search))
                    || s.MaMauSanPhamNavigation.MaDanhMucNavigation.TenDanhMuc.Contains(search));
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
            ViewBag.DsAnh = await LayDanhSachAnhAsync(s.MaMauSanPham);
            return View(s);
        }

        // Tạo/sửa sản phẩm: Quản lý + NV kho + NV bán hàng (chặn NV giao hàng).
        // Xóa hẳn vẫn chỉ Quản lý (policy riêng ở action Xoa).
        [HttpGet, Authorize(Policy = "SuaSanPham")]
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

        [HttpPost, Authorize(Policy = "SuaSanPham"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SanPhamCreateVm vm)
        {
            ViewBag.DanhMucs = await _context.DanhMucs.Select(d => d.TenDanhMuc).ToListAsync();
            ViewBag.Khus = await _context.KhuVucKhos.OrderBy(k => k.TenKhuVuc).ToListAsync();

            // Kiểm tra các file ảnh đã chọn: đúng định dạng, không vượt dung lượng, không quá số lượng.
            KiemTraAnh(vm.HinhAnhs, nameof(vm.HinhAnhs));

            // Ô số nhập chữ/số thập phân thì báo lại rõ ràng
            QuanLyKhoWeb.Services.KiemTraDauVao.BaoLoiSoNguyen(ModelState, nameof(vm.GiaTien),
                "Giá tiền phải là số lớn hơn hoặc bằng 0 (không nhập chữ).",
                nameof(vm.GiaTien));

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
            // lộ tên file gốc từ máy nhân viên). Ảnh đầu tiên là ảnh đại diện.
            var dsFileMoi = await LuuAnhAsync(vm.HinhAnhs);
            var dsDuongDanAnh = SapXepAnh(vm.ThuTuAnh, dsFileMoi, new List<string>(), vm.CoSapXepAnh == "1");
            string? duongDanHinhAnh = dsDuongDanAnh.FirstOrDefault();

            var maNguoi = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
            try
            {
                using var conn = (SqlConnection)_factory.CreateConnection();
                var p = new DynamicParameters();
                p.Add("@ten_san_pham", vm.TenSanPham);
                p.Add("@gia_tien", vm.GiaTien!.Value);
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

                // Ghi danh sách ảnh theo đúng thứ tự đã chọn (0 = ảnh đại diện).
                if (maSkuMoi > 0 && dsDuongDanAnh.Count > 0)
                {
                    var maMau = await conn.ExecuteScalarAsync<int?>(
                        "SELECT ma_mau_san_pham FROM san_pham_sku WHERE ma_sku = @ma_sku",
                        new { ma_sku = maSkuMoi });
                    if (maMau.HasValue)
                        await LuuDanhSachAnhAsync(conn, maMau.Value, dsDuongDanAnh);
                }

                // Ảnh đã ghi ra đĩa nhưng người dùng bỏ khỏi danh sách trước khi lưu -> dọn rác
                XoaFileAnh(LayFileDaGhi(dsFileMoi).Where(a => !dsDuongDanAnh.Contains(a)));

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
                // Lưu DB thất bại: dọn ảnh vừa ghi ra đĩa cho khỏi rác
                XoaFileAnh(LayFileDaGhi(dsFileMoi));
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }

        // Sửa thông tin sản phẩm: đổi tên, phân loại, giá, tồn, ảnh...
        // Mã SKU và lô hàng chốt từ lúc tạo nên chỉ hiển thị, không cho sửa.
            // Đổi số lượng tay được ghi vào nhật ký tồn kho (DIEU_CHINH_TAY) để còn vết truy xuất.
        [HttpGet, Authorize(Policy = "SuaSanPham")]
        public async Task<IActionResult> Edit(int id)
        {
            var s = await _context.SanPhamSkus
                .Include(x => x.MaMauSanPhamNavigation).ThenInclude(m => m.MaDanhMucNavigation)
                .Include(x => x.MaLoHangNavigation)
                .FirstOrDefaultAsync(x => x.MaSku == id);
            if (s == null) return NotFound();
            var m = s.MaMauSanPhamNavigation;
            ViewBag.DanhMucs = await _context.DanhMucs.Select(d => d.TenDanhMuc).ToListAsync();
            var dsAnh = await LayDanhSachAnhAsync(m.MaMauSanPham);
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
                GiaTien = s.GiaTien,
                SoLuong = s.SoLuong,
                MoTa = m.MoTa,
                HinhAnhHienTai = string.Join("|", dsAnh)
            });
        }

        [HttpPost, Authorize(Policy = "SuaSanPham"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SanPhamEditVm vm)
        {
            var s = await _context.SanPhamSkus
                .Include(x => x.MaMauSanPhamNavigation).ThenInclude(m => m.MaDanhMucNavigation)
                .FirstOrDefaultAsync(x => x.MaSku == id);
            if (s == null) return NotFound();
            ViewBag.DanhMucs = await _context.DanhMucs.Select(d => d.TenDanhMuc).ToListAsync();

            // Kiểm tra danh sách ảnh mới thêm (nếu có)
            KiemTraAnh(vm.HinhAnhMois, nameof(vm.HinhAnhMois));

            QuanLyKhoWeb.Services.KiemTraDauVao.BaoLoiSoNguyen(ModelState, nameof(vm.SoLuong),
                "Số lượng phải là số nguyên lớn hơn hoặc bằng 0 (không nhập chữ hay số thập phân).",
                nameof(vm.SoLuong));

            // Ảnh đang có của sản phẩm (dùng để kiểm tra số lượng và lọc đường dẫn lạ)
            var dsAnhCu = await LayDanhSachAnhAsync(s.MaMauSanPham);
            // JS đã sắp xếp ảnh trên giao diện thì tin #anhThuTu, kể cả khi rỗng
            // (người dùng bỏ hết ảnh) — tránh trường hợp xoá ảnh mà lưu vẫn còn.
            var coThongTinThuTu = vm.CoSapXepAnh == "1";
            // Danh sách ảnh cũ người dùng còn giữ, theo đúng thứ tự đã sắp trên giao diện.
            // Chỉ nhận đường dẫn đang thuộc sản phẩm này (chống chèn đường dẫn lạ).
            var dsAnhGiu = SapXepAnh(vm.ThuTuAnh, new List<string?>(), dsAnhCu, coThongTinThuTu)
                .Where(dsAnhCu.Contains).Distinct().ToList();
            var soAnhMoi = vm.HinhAnhMois?.Count(f => f != null && f.Length > 0) ?? 0;
            if (dsAnhGiu.Count + soAnhMoi > SoAnhToiDa)
                ModelState.AddModelError(nameof(vm.HinhAnhMois), $"Mỗi sản phẩm chỉ được tối đa {SoAnhToiDa} ảnh.");

            if (!ModelState.IsValid)
            {
                // Hiện lại form: file mới không giữ được, chỉ dựng lại ảnh cũ theo thứ tự đã giữ.
                vm.HinhAnhHienTai = string.Join("|", dsAnhGiu);
                return View(vm);
            }

            var m = s.MaMauSanPhamNavigation;
            List<string?> dsAnhThemFile = new();
            List<string> dsAnhSau = new();
            List<string> dsAnhThem = new();

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
                s.GiaTien = vm.GiaTien;

                // Ảnh: sắp theo thứ tự người dùng chọn; ảnh đầu tiên làm ảnh đại diện.
                dsAnhThemFile = await LuuAnhAsync(vm.HinhAnhMois);
                dsAnhThem = LayFileDaGhi(dsAnhThemFile);
                dsAnhSau = SapXepAnh(vm.ThuTuAnh, dsAnhThemFile, dsAnhCu, coThongTinThuTu);
                await CapNhatAnhAsync(m, dsAnhSau);

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
                // Rollback thì dọn luôn file ảnh vừa ghi ra đĩa cho khỏi rác
                XoaFileAnh(dsAnhThem);
                // Dựng lại lưới ảnh theo đúng lựa chọn của người dùng (không khôi phục ảnh đã xoá)
                vm.HinhAnhHienTai = string.Join("|", dsAnhGiu);
                ModelState.AddModelError(string.Empty, "Lưu thất bại: " + ex.Message);
                return View(vm);
            }

            // Dọn file ảnh đã bị người dùng bỏ ra khỏi danh sách (chỉ trong thư mục products)
            XoaFileAnh(dsAnhCu.Where(a => !dsAnhSau.Contains(a)));
            // Ảnh mới đã ghi ra đĩa nhưng người dùng bỏ trước khi lưu -> dọn rác
            XoaFileAnh(dsAnhThem.Where(a => !dsAnhSau.Contains(a)));

            TempData["ThongBao"] = $"Đã cập nhật sản phẩm {s.MaCodeSku}.";
            return RedirectToAction(nameof(Details), new { id = s.MaSku });
        }

        // ===== Ảnh sản phẩm (nhiều ảnh, ảnh đầu tiên là ảnh đại diện) =====

        // Kiểm tra định dạng, dung lượng và số lượng ảnh người dùng chọn.
        private void KiemTraAnh(List<IFormFile>? files, string tenTruong)
        {
            if (files == null || files.Count == 0) return;
            var hopLe = files.Where(f => f != null && f.Length > 0).ToList();
            if (hopLe.Count > SoAnhToiDa)
            {
                ModelState.AddModelError(tenTruong, $"Mỗi sản phẩm chỉ được tối đa {SoAnhToiDa} ảnh.");
                return;
            }
            foreach (var f in hopLe)
            {
                if (f.Length > GioiHanDungLuongAnh)
                {
                    ModelState.AddModelError(tenTruong, $"Ảnh \"{f.FileName}\" vượt quá 5MB, vui lòng chọn ảnh nhỏ hơn.");
                    return;
                }
                if (string.IsNullOrEmpty(f.ContentType) || !f.ContentType.StartsWith("image/"))
                {
                    ModelState.AddModelError(tenTruong, $"File \"{f.FileName}\" không phải là hình ảnh.");
                    return;
                }
            }
        }

        // Ghi các file ảnh lên đĩa với tên ngẫu nhiên. Kết quả giữ đúng số phần tử và
        // đúng vị trí của danh sách file (file rỗng/không hợp lệ -> null) để token
        // "moi:N" trong #anhThuTu ánh xạ chính xác.
        private async Task<List<string?>> LuuAnhAsync(List<IFormFile>? files)
        {
            var kq = new List<string?>();
            if (files == null || files.Count == 0) return kq;
            Directory.CreateDirectory(ThuMucAnh);
            foreach (var f in files)
            {
                if (f == null || f.Length <= 0) { kq.Add(null); continue; }
                var tenFile = Guid.NewGuid().ToString("N") + Path.GetExtension(f.FileName);
                using (var stream = new FileStream(Path.Combine(ThuMucAnh, tenFile), FileMode.Create))
                {
                    await f.CopyToAsync(stream);
                }
                kq.Add("/images/products/" + tenFile);
            }
            return kq;
        }

        // Sắp xếp lại đường dẫn ảnh theo chuỗi thứ tự người dùng chọn trên giao diện.
        // Token "moi:N" = file thứ N vừa tải lên; token khác = đường dẫn ảnh cũ đã có.
        // dsCu dùng để lọc đường dẫn lạ; trả về danh sách đã sắp, bỏ token không hợp lệ.
        // coThongTin = JS đã điền #anhThuTu: khi đó chuỗi rỗng nghĩa là người dùng bỏ hết ảnh,
        // KHÔNG được giữ lại ảnh cũ. Không có cờ (JS lỗi/tắt) thì mới giữ ảnh cũ cho an toàn.
        private static List<string> SapXepAnh(string? thuTuAnh, List<string?> dsMoi, List<string> dsCu,
            bool coThongTin = false)
        {
            var kq = new List<string>();
            if (!coThongTin && string.IsNullOrWhiteSpace(thuTuAnh))
            {
                // Không có thông tin thứ tự (JS lỗi/tắt): giữ ảnh cũ trước, ảnh mới sau
                kq.AddRange(dsCu);
                kq.AddRange(dsMoi.Where(x => !string.IsNullOrEmpty(x))!);
                return kq;
            }

            if (string.IsNullOrWhiteSpace(thuTuAnh)) return kq; // người dùng đã bỏ hết ảnh

            foreach (var token in thuTuAnh.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string? them = null;
                if (token.StartsWith("moi:", StringComparison.Ordinal))
                {
                    if (int.TryParse(token.Substring(4), out var i) && i >= 0 && i < dsMoi.Count)
                        them = dsMoi[i];
                }
                else if (dsCu.Contains(token))
                {
                    them = token;
                }
                if (!string.IsNullOrEmpty(them) && !kq.Contains(them)) kq.Add(them);
            }
            return kq;
        }

        // Các file ảnh vừa ghi ra đĩa (bỏ null) để dọn khi cần.
        private static List<string> LayFileDaGhi(List<string?> dsMoi) =>
            dsMoi.Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).ToList();

        // Xóa file ảnh vật lý (chỉ trong thư mục products để an toàn).
        private void XoaFileAnh(IEnumerable<string> duongDans)
        {
            foreach (var d in duongDans)
            {
                if (string.IsNullOrEmpty(d) || !d.StartsWith("/images/products/")) continue;
                try
                {
                    var duongDanDay = Path.Combine(_env.WebRootPath, d.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(duongDanDay)) System.IO.File.Delete(duongDanDay);
                }
                catch { /* xóa file thất bại thì bỏ qua */ }
            }
        }

        // Danh sách ảnh hiện có của một sản phẩm theo đúng thứ tự hiển thị.
        private async Task<List<string>> LayDanhSachAnhAsync(int maMauSanPham)
        {
            var ds = await _context.HinhAnhSanPhams
                .Where(h => h.MaMauSanPham == maMauSanPham)
                .OrderBy(h => h.ThuTu).ThenBy(h => h.MaHinhAnh)
                .Select(h => h.DuongDan)
                .ToListAsync();
            // Sản phẩm cũ chỉ có 1 ảnh trong mau_san_pham: dùng tạm cột đó
            if (ds.Count == 0)
            {
                var cu = await _context.MauSanPhams
                    .Where(x => x.MaMauSanPham == maMauSanPham)
                    .Select(x => x.DuongDanHinhAnh)
                    .FirstOrDefaultAsync();
                if (!string.IsNullOrEmpty(cu)) ds.Add(cu);
            }
            return ds;
        }

        // Ghi danh sách ảnh (đã có sẵn trong DB) theo thứ tự: xóa hết rồi thêm lại.
        // Dùng chung connection để nằm trong transaction đang mở của Create.
        private static async Task LuuDanhSachAnhAsync(SqlConnection conn, int maMauSanPham, List<string> duongDans)
        {
            await conn.ExecuteAsync("DELETE FROM hinh_anh_san_pham WHERE ma_mau_san_pham = @ma",
                new { ma = maMauSanPham });
            for (var i = 0; i < duongDans.Count; i++)
            {
                await conn.ExecuteAsync(
                    "INSERT INTO hinh_anh_san_pham (ma_mau_san_pham, duong_dan, thu_tu) VALUES (@ma, @dd, @tt)",
                    new { ma = maMauSanPham, dd = duongDans[i], tt = i });
            }
        }

        // Cập nhật danh sách ảnh qua EF (dùng trong transaction của Edit), đồng thời
        // chép ảnh đầu tiên sang cột ảnh đại diện của mau_san_pham.
        private async Task CapNhatAnhAsync(QuanLyKhoWeb.Models.Entities.MauSanPham m, List<string> duongDans)
        {
            var cu = await _context.HinhAnhSanPhams
                .Where(h => h.MaMauSanPham == m.MaMauSanPham)
                .ToListAsync();
            _context.HinhAnhSanPhams.RemoveRange(cu);
            for (var i = 0; i < duongDans.Count; i++)
            {
                _context.HinhAnhSanPhams.Add(new QuanLyKhoWeb.Models.Entities.HinhAnhSanPham
                {
                    MaMauSanPham = m.MaMauSanPham,
                    DuongDan = duongDans[i],
                    ThuTu = i
                });
            }
            m.DuongDanHinhAnh = duongDans.FirstOrDefault();
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
        [HttpPost, Authorize(Policy = "SuaSanPham"), ValidateAntiForgeryToken]
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
            var maCode = s.MaCodeSku;
            // Ảnh của sản phẩm: dòng trong hinh_anh_san_pham (xóa theo khóa ngoại cascade)
            // + cột ảnh đại diện cũ. Gom lại để dọn file sau khi DB lưu thành công.
            var dsAnhCanXoa = mau == null ? new List<string>() : await LayDanhSachAnhAsync(mau.MaMauSanPham);
            if (!string.IsNullOrEmpty(mau?.DuongDanHinhAnh)) dsAnhCanXoa.Add(mau.DuongDanHinhAnh);
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
                XoaFileAnh(dsAnhCanXoa);
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