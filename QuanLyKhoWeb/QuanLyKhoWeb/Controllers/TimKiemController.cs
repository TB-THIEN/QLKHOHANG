using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Models.ViewModels;

namespace QuanLyKhoWeb.Controllers
{
    // Gợi ý cho ô tìm kiếm riêng của từng trang: mỗi trang trả đúng loại dữ liệu
    // trang đó đang hiển thị, mỗi gợi ý trỏ tới bản ghi chi tiết tương ứng.
    public class TimKiemController : Controller
    {
        private const int SoGoiY = 8;
        private readonly DoAnQlkContext _context;

        public TimKiemController(DoAnQlkContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GoiYTrang(string? trang, string? q)
        {
            var term = (q ?? string.Empty).Trim();
            if (term.Length == 0) return Json(Array.Empty<GoiYTimKiemVm>());
            if (term.Length > 80) term = term[..80];

            // Gợi ý cũng phải theo quyền: không trả dữ liệu của trang người dùng không mở được
            var khoaTrang = (trang ?? "").ToLowerInvariant();
            if (khoaTrang == "baocao" && !QuanLyKhoWeb.Authorization.QuyenNguoiDung.XemBaoCao(User))
                return Json(Array.Empty<GoiYTimKiemVm>());
            if (khoaTrang == "nhanvien" && !QuanLyKhoWeb.Authorization.QuyenNguoiDung.QuanLyNhanVien(User))
                return Json(Array.Empty<GoiYTimKiemVm>());

            var kq = new List<GoiYTimKiemVm>();

            void Them(string nhom, string icon, string tieuDe, string? moTa, string? url)
            {
                if (string.IsNullOrEmpty(url)) return;
                kq.Add(new GoiYTimKiemVm { Nhom = nhom, Icon = icon, TieuDe = tieuDe, MoTa = moTa, Url = url });
            }

            switch ((trang ?? "").ToLowerInvariant())
            {
                case "tongquan":
                case "donhang":
                case "baocao":
                {
                    var rows = await _context.DonHangs.AsNoTracking()
                        .Where(d => EF.Functions.Collate(d.MaCodeDonHang, "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(d.TenKhachHangGiao ?? "", "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(d.SdtKhachHangGiao ?? "", "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(d.DiaChiGiaoHang ?? "", "Vietnamese_CI_AI").Contains(term))
                        .OrderByDescending(d => d.NgayDatHang)
                        .Take(SoGoiY)
                        .Select(d => new
                        {
                            d.MaDonHang, d.MaCodeDonHang, d.TenKhachHangGiao, d.SdtKhachHangGiao, d.TrangThai,
                            NhanVien = d.MaNhanVienNavigation != null ? d.MaNhanVienNavigation.HoTen : null
                        })
                        .ToListAsync();
                    foreach (var d in rows)
                        Them("Đơn hàng", "bi-receipt", $"Đơn {d.MaCodeDonHang}",
                            Gop(d.TenKhachHangGiao, d.SdtKhachHangGiao, d.NhanVien, TenTrangThaiDon(d.TrangThai)),
                            Url.Action("Details", "DonHang", new { id = d.MaDonHang }));
                    break;
                }
                case "sanpham":
                case "tonkho":
                {
                    var rows = await _context.SanPhamSkus.AsNoTracking()
                        .Where(s => EF.Functions.Collate(s.MaCodeSku, "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(s.MaMauSanPhamNavigation.TenSanPham, "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(s.MaMauSanPhamNavigation.NhanHieu ?? "", "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(s.MaMauSanPhamNavigation.ChatLieu ?? "", "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(s.MaMauSanPhamNavigation.MauSac ?? "", "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(s.MaMauSanPhamNavigation.LoaiSanPham ?? "", "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(s.MaMauSanPhamNavigation.MaDanhMucNavigation.TenDanhMuc, "Vietnamese_CI_AI").Contains(term))
                        .OrderBy(s => s.MaMauSanPhamNavigation.TenSanPham)
                        .Take(SoGoiY)
                        .Select(s => new
                        {
                            s.MaSku, s.MaCodeSku, s.SoLuong,
                            Ten = s.MaMauSanPhamNavigation.TenSanPham,
                            DanhMuc = s.MaMauSanPhamNavigation.MaDanhMucNavigation.TenDanhMuc,
                            s.MaMauSanPhamNavigation.ChatLieu,
                            s.MaMauSanPhamNavigation.MauSac,
                            ViTri = s.TonKhoKeHangs.Select(t => t.MaKeHangNavigation.MaKhuVucNavigation.TenKhuVuc + " / " + t.MaKeHangNavigation.TenKeHang)
                        })
                        .ToListAsync();
                    foreach (var s in rows)
                        Them("Sản phẩm", "bi-box-seam", s.Ten,
                            Gop(s.MaCodeSku, s.DanhMuc, s.ChatLieu, s.MauSac, $"Tồn: {s.SoLuong}",
                                s.ViTri.Any() ? string.Join(", ", s.ViTri.Take(2)) : null),
                            Url.Action("Details", "SanPham", new { id = s.MaSku }));
                    break;
                }
                case "phieunhap":
                {
                    var rows = await _context.PhieuNhapKhos.AsNoTracking()
                        .Where(p => EF.Functions.Collate(p.MaCodePhieuNhap, "Vietnamese_CI_AI").Contains(term)
                            || (p.MaNhaCungCapNavigation != null
                                && EF.Functions.Collate(p.MaNhaCungCapNavigation.TenNhaCungCap, "Vietnamese_CI_AI").Contains(term))
                            || (p.NguoiTaoNavigation != null
                                && EF.Functions.Collate(p.NguoiTaoNavigation.HoTen, "Vietnamese_CI_AI").Contains(term))
                            || EF.Functions.Collate(p.GhiChu ?? "", "Vietnamese_CI_AI").Contains(term))
                        .OrderByDescending(p => p.NgayTao)
                        .Take(SoGoiY)
                        .Select(p => new
                        {
                            p.MaPhieuNhap, p.MaCodePhieuNhap, p.TrangThai, p.NgayNhap,
                            Ncc = p.MaNhaCungCapNavigation != null ? p.MaNhaCungCapNavigation.TenNhaCungCap : null,
                            NguoiTao = p.NguoiTaoNavigation != null ? p.NguoiTaoNavigation.HoTen : null
                        })
                        .ToListAsync();
                    foreach (var p in rows)
                        Them("Phiếu nhập", "bi-box-arrow-in-down", $"Phiếu {p.MaCodePhieuNhap}",
                            Gop(p.Ncc, p.NguoiTao, TenTrangThaiPhieu(p.TrangThai), p.NgayNhap?.ToString("dd/MM/yyyy")),
                            Url.Action("ChiTietPhieu", "TonKho", new { id = p.MaPhieuNhap }));
                    break;
                }
                case "ncc":
                {
                    var rows = await _context.NhaCungCaps.AsNoTracking()
                        .Where(n => EF.Functions.Collate(n.MaCodeNcc, "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(n.TenNhaCungCap, "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(n.SoDienThoai ?? "", "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(n.Email ?? "", "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(n.DiaChi ?? "", "Vietnamese_CI_AI").Contains(term))
                        .OrderBy(n => n.TenNhaCungCap)
                        .Take(SoGoiY)
                        .Select(n => new { n.MaNhaCungCap, n.MaCodeNcc, n.TenNhaCungCap, n.SoDienThoai, n.DiaChi, n.TrangThaiHoatDong })
                        .ToListAsync();
                    foreach (var n in rows)
                        Them("Nhà cung cấp", "bi-building", n.TenNhaCungCap,
                            Gop(n.MaCodeNcc, n.SoDienThoai, n.DiaChi, n.TrangThaiHoatDong == true ? "Đang hợp tác" : "Ngừng hợp tác"),
                            Url.Action("Details", "NhaCungCap", new { id = n.MaNhaCungCap }));
                    break;
                }
                case "khuyenmai":
                {
                    var rows = await _context.KhuyenMais.AsNoTracking()
                        .Where(k => EF.Functions.Collate(k.MaCodeKhuyenMai, "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(k.TenKhuyenMai, "Vietnamese_CI_AI").Contains(term))
                        .OrderByDescending(k => k.NgayBatDau)
                        .Take(SoGoiY)
                        .Select(k => new { k.MaKhuyenMai, k.MaCodeKhuyenMai, k.TenKhuyenMai, k.TrangThaiHoatDong, k.NgayBatDau, k.NgayKetThuc })
                        .ToListAsync();
                    foreach (var k in rows)
                        Them("Khuyến mãi", "bi-ticket-perforated", k.TenKhuyenMai,
                            Gop(k.MaCodeKhuyenMai, k.TrangThaiHoatDong == true ? "Đang hoạt động" : "Đã tắt",
                                $"{k.NgayBatDau:dd/MM/yyyy} - {k.NgayKetThuc:dd/MM/yyyy}"),
                            Url.Action("Details", "KhuyenMai", new { id = k.MaKhuyenMai }));
                    break;
                }
                case "nhanvien":
                {
                    var rows = await _context.NguoiDungs.AsNoTracking()
                        .Where(n => EF.Functions.Collate(n.HoTen, "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(n.TenDangNhap, "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(n.Email ?? "", "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(n.SoDienThoai ?? "", "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(n.Cccd ?? "", "Vietnamese_CI_AI").Contains(term))
                        .OrderBy(n => n.HoTen)
                        .Take(SoGoiY)
                        .Select(n => new { n.MaNguoiDung, n.HoTen, n.TenDangNhap, n.SoDienThoai, VaiTro = n.MaVaiTroNavigation.TenVaiTro })
                        .ToListAsync();
                    foreach (var n in rows)
                        Them("Nhân viên", "bi-person-badge", n.HoTen,
                            Gop(n.TenDangNhap, n.VaiTro, n.SoDienThoai),
                            Url.Action("Details", "NhanVien", new { id = n.MaNguoiDung }));
                    break;
                }
                case "vanchuyen":
                {
                    var rows = await _context.VanDons.AsNoTracking()
                        .Where(v => EF.Functions.Collate(v.MaCodeVanDon, "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(v.MaDonHangNavigation.MaCodeDonHang, "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(v.MaDonHangNavigation.TenKhachHangGiao ?? "", "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(v.MaDonHangNavigation.SdtKhachHangGiao ?? "", "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(v.MaDonHangNavigation.DiaChiGiaoHang ?? "", "Vietnamese_CI_AI").Contains(term)
                            || (v.MaNhanVienGiaoHangNavigation != null
                                && EF.Functions.Collate(v.MaNhanVienGiaoHangNavigation.HoTen, "Vietnamese_CI_AI").Contains(term)))
                        .OrderByDescending(v => v.NgayTao)
                        .Take(SoGoiY)
                        .Select(v => new
                        {
                            v.MaVanDon, v.MaCodeVanDon, v.TrangThai,
                            MaDon = v.MaDonHangNavigation.MaCodeDonHang,
                            Khach = v.MaDonHangNavigation.TenKhachHangGiao,
                            Nv = v.MaNhanVienGiaoHangNavigation != null ? v.MaNhanVienGiaoHangNavigation.HoTen : null
                        })
                        .ToListAsync();
                    foreach (var v in rows)
                        Them("Vận đơn", "bi-truck", $"Vận đơn {v.MaCodeVanDon}",
                            Gop($"Đơn {v.MaDon}", v.Khach, v.Nv, TenTrangThaiVanDon(v.TrangThai)),
                            Url.Action("GanNhanVien", "VanChuyen", new { id = v.MaVanDon }));
                    break;
                }
                case "khuhang":
                {
                    // Gợi ý cả khu, kệ và hàng hóa để tìm kiếm bao trọn trang khu hàng.
                    var khus = await _context.KhuVucKhos.AsNoTracking()
                        .Where(k => EF.Functions.Collate(k.TenKhuVuc, "Vietnamese_CI_AI").Contains(term)
                            || k.KeHangs.Any(ke => EF.Functions.Collate(ke.TenKeHang, "Vietnamese_CI_AI").Contains(term))
                            || k.TonKhoKhus.Any(t => EF.Functions.Collate(t.MaSkuNavigation.MaCodeSku, "Vietnamese_CI_AI").Contains(term)
                                || EF.Functions.Collate(t.MaSkuNavigation.MaMauSanPhamNavigation.TenSanPham, "Vietnamese_CI_AI").Contains(term))
                            || k.KeHangs.Any(ke => ke.TonKhoKeHangs.Any(t => EF.Functions.Collate(t.MaSkuNavigation.MaCodeSku, "Vietnamese_CI_AI").Contains(term)
                                || EF.Functions.Collate(t.MaSkuNavigation.MaMauSanPhamNavigation.TenSanPham, "Vietnamese_CI_AI").Contains(term))))
                        .OrderBy(k => k.TenKhuVuc)
                        .Take(SoGoiY)
                        .Select(k => new { k.MaKhuVuc, k.TenKhuVuc, SoKe = k.KeHangs.Count() })
                        .ToListAsync();
                    foreach (var k in khus)
                        Them("Khu hàng", "bi-diagram-3", k.TenKhuVuc,
                            Gop($"{k.SoKe} kệ"),
                            Url.Action("Index", "KhuHang", new { search = k.TenKhuVuc }));

                    var kes = await _context.KeHangs.AsNoTracking()
                        .Where(ke => EF.Functions.Collate(ke.TenKeHang, "Vietnamese_CI_AI").Contains(term))
                        .OrderBy(ke => ke.TenKeHang)
                        .Take(SoGoiY)
                        .Select(ke => new { ke.MaKeHang, ke.TenKeHang, Khu = ke.MaKhuVucNavigation.TenKhuVuc })
                        .ToListAsync();
                    foreach (var ke in kes)
                        Them("Kệ hàng", "bi-grid-3x3-gap", $"{ke.Khu} / {ke.TenKeHang}",
                            "Bấm để lọc sơ đồ theo kệ này",
                            Url.Action("Index", "KhuHang", new { search = ke.TenKeHang }));

                    var hangs = await _context.SanPhamSkus.AsNoTracking()
                        .Where(s => EF.Functions.Collate(s.MaCodeSku, "Vietnamese_CI_AI").Contains(term)
                            || EF.Functions.Collate(s.MaMauSanPhamNavigation.TenSanPham, "Vietnamese_CI_AI").Contains(term))
                        .OrderBy(s => s.MaMauSanPhamNavigation.TenSanPham)
                        .Take(SoGoiY)
                        .Select(s => new { s.MaSku, s.MaCodeSku, s.SoLuong, Ten = s.MaMauSanPhamNavigation.TenSanPham })
                        .ToListAsync();
                    foreach (var s in hangs)
                        Them("Hàng hóa", "bi-box-seam", s.Ten,
                            Gop(s.MaCodeSku, $"Tồn: {s.SoLuong}"),
                            Url.Action("Index", "KhuHang", new { search = s.MaCodeSku }));
                    break;
                }
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
