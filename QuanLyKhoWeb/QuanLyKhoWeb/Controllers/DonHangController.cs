using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Models.Entities;
using QuanLyKhoWeb.Models.ViewModels;
using QuanLyKhoWeb.Services.Interfaces;
using System.Security.Claims;

namespace QuanLyKhoWeb.Controllers
{
    public class DonHangController : Controller
    {
        private readonly DoAnQlkContext _context;
        private readonly IDonHangService _donHangService;
        public DonHangController(DoAnQlkContext context, IDonHangService donHangService)
        {
            _context = context;
            _donHangService = donHangService;
        }

        // Danh sách: mã đơn, sản phẩm, số lượng, tổng tiền, chức năng (chi tiết + hủy + hoàn thành) + lọc thời gian/hình thức
        public async Task<IActionResult> Index(string? search, DateTime? tuNgay, DateTime? denNgay, string? hinhThuc)
        {
            ViewData["SearchValue"] = search;
            ViewData["SearchController"] = "DonHang";
            ViewBag.TuNgay = tuNgay?.ToString("yyyy-MM-dd");
            ViewBag.DenNgay = denNgay?.ToString("yyyy-MM-dd");
            ViewBag.HinhThuc = hinhThuc;

            var q = _context.DonHangs
                .Include(d => d.ChiTietDonHangs).ThenInclude(ct => ct.MaSkuNavigation).ThenInclude(s => s.MaMauSanPhamNavigation)
                .Include(d => d.MaNhanVienNavigation)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(d => d.MaCodeDonHang.Contains(search)
                    || (d.TenKhachHangGiao ?? "").Contains(search)
                    || (d.SdtKhachHangGiao ?? "").Contains(search));

            if (tuNgay.HasValue)
                q = q.Where(d => d.NgayDatHang >= tuNgay.Value.Date);
            if (denNgay.HasValue)
            {
                var end = denNgay.Value.Date.AddDays(1);
                q = q.Where(d => d.NgayDatHang < end);
            }
            if (hinhThuc == "TAI_SHOP" || hinhThuc == "GIAO_HANG")
                q = q.Where(d => d.HinhThucNhanHang == hinhThuc);

            var list = await q.OrderByDescending(d => d.NgayDatHang).Take(200).ToListAsync();
            return View(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var dh = await _context.DonHangs
                .Include(d => d.ChiTietDonHangs).ThenInclude(ct => ct.MaSkuNavigation).ThenInclude(s => s.MaMauSanPhamNavigation)
                .Include(d => d.MaKhuyenMaiNavigation)
                .Include(d => d.MaNhanVienNavigation)
                .Include(d => d.PhanBoXuatKhoTheoKes).ThenInclude(p => p.MaKeHangNavigation).ThenInclude(k => k.MaKhuVucNavigation)
                .FirstOrDefaultAsync(d => d.MaDonHang == id);
            if (dh == null) return NotFound();
            return View(dh);
        }

        // Tạo đơn: bỏ nguồn đơn/khách hàng panel cũ, thêm Mã giảm giá listdown, TT bổ sung có tên/SĐT/địa chỉ, mã tự sinh
        // Chỉ Quản lý + NV kho + NV bán hàng được tạo (chặn NV giao hàng)
        [HttpGet, Authorize(Policy = "DuocTao")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Skus = await _context.SanPhamSkus
                .Include(s => s.MaMauSanPhamNavigation)
                .Where(s => s.SoLuong > 0)
                .Take(100).ToListAsync();

            var now = DateTime.Now;
            ViewBag.KhuyenMais = await _context.KhuyenMais
                .Where(k => k.TrangThaiHoatDong == true && k.NgayBatDau <= now && k.NgayKetThuc >= now)
                .OrderBy(k => k.MaCodeKhuyenMai).ToListAsync();

            return View(new DonHangCreateVm());
        }

        public async Task<IActionResult> TimSku(string term)
        {
            // Không nhập gì -> trả tối đa 60 món để lướt chọn; có từ khóa -> 10 gợi ý khớp nhất
            var layBaoNhieu = string.IsNullOrEmpty(term) ? 60 : 10;
            var list = await _context.SanPhamSkus
                .Include(s => s.MaMauSanPhamNavigation)
                .Where(s => s.SoLuong > 0 && (string.IsNullOrEmpty(term) ||
                    s.MaCodeSku.Contains(term) || s.MaMauSanPhamNavigation.TenSanPham.Contains(term)))
                .Take(layBaoNhieu)
                .Select(s => new { s.MaSku, s.MaCodeSku, ten = s.MaMauSanPhamNavigation.TenSanPham, s.GiaNiemYet, s.SoLuong })
                .ToListAsync();
            return Json(list);
        }

        [HttpPost, Authorize(Policy = "DuocTao"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DonHangCreateVm vm, List<int> MaSkus, List<int> SoLuongs)
        {
            var now = DateTime.Now;
            ViewBag.Skus = await _context.SanPhamSkus.Include(s => s.MaMauSanPhamNavigation).Where(s => s.SoLuong > 0).Take(100).ToListAsync();
            ViewBag.KhuyenMais = await _context.KhuyenMais.Where(k => k.TrangThaiHoatDong == true && k.NgayBatDau <= now && k.NgayKetThuc >= now).ToListAsync();

            if (MaSkus == null || !MaSkus.Any())
            {
                ModelState.AddModelError(string.Empty, "Vui lòng chọn ít nhất 1 sản phẩm.");
                return View(vm);
            }
            // Ô số lượng nhập chữ/số thập phân -> binding hỏng, báo lại tiếng Việt
            // thay vì lặng lẽ lấy số lượng mặc định
            QuanLyKhoWeb.Services.KiemTraDauVao.BaoLoiSoNguyen(ModelState, "SoLuongs",
                "Số lượng phải là số nguyên lớn hơn 0 (không nhập chữ hay số thập phân).",
                "SoLuongs", "MaSkus");

            // Build chi tiết từ MaSkus/SoLuongs (chỉ ma_sku + so_luong - sp_TaoDonHang tự lấy
            // đơn giá từ gia_niem_yet và tự tính tiền giảm theo mã khuyến mãi, không nhận từ client)
            var items = new List<TaoDonHangItem>();
            for (int i = 0; i < MaSkus.Count; i++)
            {
                int maSku = MaSkus[i];
                int sl = (SoLuongs != null && i < SoLuongs.Count) ? SoLuongs[i] : 1;
                if (sl < 1) sl = 1;
                var sku = await _context.SanPhamSkus.FindAsync(maSku);
                if (sku == null) continue;
                if (sku.SoLuong < sl)
                {
                    ModelState.AddModelError(string.Empty, $"SKU {sku.MaCodeSku} chỉ còn {sku.SoLuong} trong kho.");
                    return View(vm);
                }
                items.Add(new TaoDonHangItem { MaSku = maSku, SoLuong = sl });
            }
            if (!items.Any())
            {
                ModelState.AddModelError(string.Empty, "Không có sản phẩm hợp lệ.");
                return View(vm);
            }
            if (!ModelState.IsValid) return View(vm);

            // [Required] không chặn chuỗi toàn khoảng trắng -> kiểm tra thêm
            if (string.IsNullOrWhiteSpace(vm.TenKhachHangGiao))
                ModelState.AddModelError(nameof(vm.TenKhachHangGiao), "Vui lòng nhập tên khách hàng.");
            if (string.IsNullOrWhiteSpace(vm.SdtKhachHangGiao))
                ModelState.AddModelError(nameof(vm.SdtKhachHangGiao), "Vui lòng nhập số điện thoại.");
            if (string.IsNullOrWhiteSpace(vm.DiaChiGiaoHang))
                ModelState.AddModelError(nameof(vm.DiaChiGiaoHang), "Vui lòng nhập địa chỉ.");
            if (!ModelState.IsValid) return View(vm);

            // Tên khách hàng không được chứa số
            if (!string.IsNullOrWhiteSpace(vm.TenKhachHangGiao)
                && !QuanLyKhoWeb.Services.KiemTraDauVao.LaTenHopLe(vm.TenKhachHangGiao))
            {
                ModelState.AddModelError(nameof(vm.TenKhachHangGiao), "Tên khách hàng không được chứa số.");
                return View(vm);
            }

            // Tên/SĐT/địa chỉ bắt buộc nhập (validate ở ViewModel); tái sử dụng bản ghi
            // khach_hang theo SĐT để đơn sau khỏi nhập lại hồ sơ.
            // Tên/SĐT/địa chỉ giao thực tế được sp_TaoDonHang lưu trực tiếp vào đơn.
            var sdt = vm.SdtKhachHangGiao!.Trim();
            var ten = vm.TenKhachHangGiao!.Trim();
            var kh = await _context.KhachHangs.FirstOrDefaultAsync(k => k.SoDienThoai == sdt);
            if (kh == null)
            {
                kh = new KhachHang
                {
                    TenKhachHang = ten,
                    SoDienThoai = sdt,
                    DiaChi = vm.DiaChiGiaoHang!.Trim()
                };
                _context.KhachHangs.Add(kh);
                await _context.SaveChangesAsync();
            }
            int maKhach = kh.MaKhachHang;

            var maNv = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var hinhThuc = vm.HinhThucNhanHang == "GIAO_HANG" ? "GIAO_HANG" : "TAI_SHOP";

            var kq = await _donHangService.TaoDonHangAsync(maNv, maKhach,
                vm.TenKhachHangGiao, vm.SdtKhachHangGiao, vm.DiaChiGiaoHang,
                vm.MaKhuyenMai, vm.PhiVanChuyen, hinhThuc, vm.GhiChu, items);

            if (!kq.ThanhCong)
            {
                ModelState.AddModelError(string.Empty, kq.ThongBao);
                return View(vm);
            }
            TempData["ThongBao"] = kq.ThongBao + (hinhThuc == "GIAO_HANG" ? " Vận đơn đã tự tạo." : "");
            return RedirectToAction(nameof(Index));
        }

        // Hủy đơn: popup xác nhận ở View, gọi sp_HuyDonHang (chỉ Quản lý có CoQuyenHuyDon, chỉ UNPAID)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Huy(int id, string? lyDoHuy)
        {
            var maNguoi = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var kq = await _donHangService.HuyDonHangAsync(id, maNguoi, lyDoHuy);
            if (kq.ThanhCong) TempData["ThongBao"] = kq.ThongBao;
            else TempData["Loi"] = kq.ThongBao;
            return RedirectToAction(nameof(Index));
        }
    }
}