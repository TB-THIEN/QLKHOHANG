using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Models.ViewModels;
using System.Security.Claims;

namespace QuanLyKhoWeb.Controllers
{
    // Tim kiem tong o thanh header: tra goi y theo nhieu loai du lieu, moi goi y tro thang
    // toi trang chi tiet tuong ung. Tach rieng khoi cac o tim kiem loc cua tung trang.
    public class TimKiemController : Controller
    {
        private const int SoGoiY = 4;
        private readonly DoAnQlkContext _context;

        public TimKiemController(DoAnQlkContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GoiY(string? q)
        {
            var term = (q ?? string.Empty).Trim();
            if (term.Length == 0) return Json(Array.Empty<GoiYTimKiemVm>());
            if (term.Length > 80) term = term[..80];

            var vaiTro = User.FindFirstValue("VaiTro") ?? string.Empty;
            var kq = new List<GoiYTimKiemVm>();

            var donHangs = await _context.DonHangs.AsNoTracking()
                .Where(d => EF.Functions.Collate(d.MaCodeDonHang, "Vietnamese_CI_AI").Contains(term)
                    || EF.Functions.Collate(d.TenKhachHangGiao ?? "", "Vietnamese_CI_AI").Contains(term)
                    || EF.Functions.Collate(d.SdtKhachHangGiao ?? "", "Vietnamese_CI_AI").Contains(term))
                .OrderByDescending(d => d.NgayDatHang)
                .Take(SoGoiY)
                .Select(d => new { d.MaDonHang, d.MaCodeDonHang, d.TenKhachHangGiao, d.SdtKhachHangGiao, d.TrangThai })
                .ToListAsync();

            foreach (var d in donHangs)
            {
                kq.Add(new GoiYTimKiemVm
                {
                    Nhom = "Đơn hàng",
                    Icon = "bi-receipt",
                    TieuDe = $"Đơn {d.MaCodeDonHang}",
                    MoTa = Gop(d.TenKhachHangGiao, d.SdtKhachHangGiao, TenTrangThaiDon(d.TrangThai)),
                    Url = Url.Action("Details", "DonHang", new { id = d.MaDonHang }) ?? "#"
                });
            }

            var skus = await _context.SanPhamSkus.AsNoTracking()
                .Where(s => EF.Functions.Collate(s.MaCodeSku, "Vietnamese_CI_AI").Contains(term)
                    || EF.Functions.Collate(s.MaMauSanPhamNavigation.TenSanPham, "Vietnamese_CI_AI").Contains(term))
                .OrderBy(s => s.MaMauSanPhamNavigation.TenSanPham)
                .Take(SoGoiY)
                .Select(s => new { s.MaSku, s.MaCodeSku, Ten = s.MaMauSanPhamNavigation.TenSanPham, s.SoLuong })
                .ToListAsync();

            foreach (var s in skus)
            {
                kq.Add(new GoiYTimKiemVm
                {
                    Nhom = "Sản phẩm",
                    Icon = "bi-box-seam",
                    TieuDe = s.Ten,
                    MoTa = Gop(s.MaCodeSku, $"Tồn kho: {s.SoLuong}"),
                    Url = Url.Action("Details", "SanPham", new { id = s.MaSku }) ?? "#"
                });
            }

            var vanDons = await _context.VanDons.AsNoTracking()
                .Where(v => EF.Functions.Collate(v.MaCodeVanDon, "Vietnamese_CI_AI").Contains(term)
                    || EF.Functions.Collate(v.MaDonHangNavigation.MaCodeDonHang, "Vietnamese_CI_AI").Contains(term))
                .OrderByDescending(v => v.NgayTao)
                .Take(3)
                .Select(v => new
                {
                    v.MaVanDon,
                    v.MaCodeVanDon,
                    v.TrangThai,
                    MaDon = v.MaDonHangNavigation.MaCodeDonHang
                })
                .ToListAsync();

            foreach (var v in vanDons)
            {
                kq.Add(new GoiYTimKiemVm
                {
                    Nhom = "Vận đơn",
                    Icon = "bi-truck",
                    TieuDe = $"Vận đơn {v.MaCodeVanDon}",
                    MoTa = Gop($"Đơn {v.MaDon}", TenTrangThaiVanDon(v.TrangThai)),
                    Url = Url.Action("GanNhanVien", "VanChuyen", new { id = v.MaVanDon }) ?? "#"
                });
            }

            var phieuNhaps = await _context.PhieuNhapKhos.AsNoTracking()
                .Where(p => EF.Functions.Collate(p.MaCodePhieuNhap, "Vietnamese_CI_AI").Contains(term))
                .OrderByDescending(p => p.NgayTao)
                .Take(3)
                .Select(p => new { p.MaPhieuNhap, p.MaCodePhieuNhap, p.TrangThai, p.NgayNhap })
                .ToListAsync();

            foreach (var p in phieuNhaps)
            {
                kq.Add(new GoiYTimKiemVm
                {
                    Nhom = "Phiếu nhập",
                    Icon = "bi-box-arrow-in-down",
                    TieuDe = $"Phiếu nhập {p.MaCodePhieuNhap}",
                    MoTa = Gop(TenTrangThaiPhieu(p.TrangThai), p.NgayNhap?.ToString("dd/MM/yyyy")),
                    Url = Url.Action("ChiTietPhieu", "TonKho", new { id = p.MaPhieuNhap }) ?? "#"
                });
            }

            if (vaiTro != "Nhân viên giao hàng")
            {
                var nccs = await _context.NhaCungCaps.AsNoTracking()
                    .Where(n => EF.Functions.Collate(n.MaCodeNcc, "Vietnamese_CI_AI").Contains(term)
                        || EF.Functions.Collate(n.TenNhaCungCap, "Vietnamese_CI_AI").Contains(term)
                        || EF.Functions.Collate(n.SoDienThoai ?? "", "Vietnamese_CI_AI").Contains(term)
                        || EF.Functions.Collate(n.Email ?? "", "Vietnamese_CI_AI").Contains(term))
                    .OrderBy(n => n.TenNhaCungCap)
                    .Take(3)
                    .Select(n => new { n.MaNhaCungCap, n.MaCodeNcc, n.TenNhaCungCap, n.SoDienThoai, n.TrangThaiHoatDong })
                    .ToListAsync();

                foreach (var n in nccs)
                {
                    kq.Add(new GoiYTimKiemVm
                    {
                        Nhom = "Nhà cung cấp",
                        Icon = "bi-building",
                        TieuDe = n.TenNhaCungCap,
                        MoTa = Gop(n.MaCodeNcc, n.SoDienThoai, n.TrangThaiHoatDong == true ? "Đang hợp tác" : "Ngừng hợp tác"),
                        Url = Url.Action("Details", "NhaCungCap", new { id = n.MaNhaCungCap }) ?? "#"
                    });
                }
            }

            if (vaiTro == "Quản lý")
            {
                var nhanViens = await _context.NguoiDungs.AsNoTracking()
                    .Where(n => EF.Functions.Collate(n.HoTen, "Vietnamese_CI_AI").Contains(term)
                        || EF.Functions.Collate(n.TenDangNhap, "Vietnamese_CI_AI").Contains(term)
                        || EF.Functions.Collate(n.Email ?? "", "Vietnamese_CI_AI").Contains(term)
                        || EF.Functions.Collate(n.SoDienThoai ?? "", "Vietnamese_CI_AI").Contains(term))
                    .OrderBy(n => n.HoTen)
                    .Take(3)
                    .Select(n => new { n.MaNguoiDung, n.HoTen, n.TenDangNhap, n.SoDienThoai })
                    .ToListAsync();

                foreach (var n in nhanViens)
                {
                    kq.Add(new GoiYTimKiemVm
                    {
                        Nhom = "Nhân viên",
                        Icon = "bi-person-badge",
                        TieuDe = n.HoTen,
                        MoTa = Gop(n.TenDangNhap, n.SoDienThoai),
                        Url = Url.Action("Details", "NhanVien", new { id = n.MaNguoiDung }) ?? "#"
                    });
                }
            }

            var khuyenMais = await _context.KhuyenMais.AsNoTracking()
                .Where(k => EF.Functions.Collate(k.MaCodeKhuyenMai, "Vietnamese_CI_AI").Contains(term)
                    || EF.Functions.Collate(k.TenKhuyenMai, "Vietnamese_CI_AI").Contains(term))
                .OrderByDescending(k => k.NgayBatDau)
                .Take(3)
                .Select(k => new { k.MaKhuyenMai, k.MaCodeKhuyenMai, k.TenKhuyenMai, k.TrangThaiHoatDong })
                .ToListAsync();

            foreach (var k in khuyenMais)
            {
                kq.Add(new GoiYTimKiemVm
                {
                    Nhom = "Khuyến mãi",
                    Icon = "bi-ticket-perforated",
                    TieuDe = k.TenKhuyenMai,
                    MoTa = Gop(k.MaCodeKhuyenMai, k.TrangThaiHoatDong == true ? "Đang hoạt động" : "Đã tắt"),
                    Url = Url.Action("Details", "KhuyenMai", new { id = k.MaKhuyenMai }) ?? "#"
                });
            }

            return Json(kq);
        }

        private static string? Gop(params string?[] parts)
        {
            var s = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private static string TenTrangThaiDon(string? trangThai) => trangThai switch
        {
            "COMPLETED" => "Hoàn tất",
            "CONFIRMED" => "Đã xác nhận",
            "CANCELLED" => "Đã hủy",
            _ => trangThai ?? ""
        };

        private static string TenTrangThaiVanDon(string? trangThai) => trangThai switch
        {
            "CHUA_HOAN_THANH" => "Chưa hoàn thành",
            "CHUA_GIAO" => "Chưa giao",
            "DA_GIAO" => "Đã giao",
            _ => trangThai ?? ""
        };

        private static string TenTrangThaiPhieu(string? trangThai) => trangThai switch
        {
            "PENDING" => "Chờ hoàn tất",
            "COMPLETED" => "Đã hoàn tất",
            "CANCELLED" => "Đã hủy",
            _ => trangThai ?? ""
        };
    }
}
