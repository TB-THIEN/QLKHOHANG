using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QuanLyKhoWeb.Authorization;
using QuanLyKhoWeb.Models.ViewModels;
using QuanLyKhoWeb.Services.Ai;
using QuanLyKhoWeb.Services.Interfaces;

namespace QuanLyKhoWeb.Controllers
{
    // Khung chat trợ lý AI. Mọi action đều yêu cầu đăng nhập (filter toàn cục) và
    // chỉ Quản lý mới được dùng. Quyền được kiểm tra lại ở đây, không tin phía trình duyệt.
    [Authorize(Policy = "ChiQuanLy")]
    public class TroLyController : Controller
    {
        private readonly AiTroLy _troLy;
        private readonly KhoHanhDongCho _khoHanhDong;
        private readonly INhanVienService _nhanVien;
        private readonly ILogger<TroLyController> _logger;

        public TroLyController(AiTroLy troLy, KhoHanhDongCho khoHanhDong,
            INhanVienService nhanVien, ILogger<TroLyController> logger)
        {
            _troLy = troLy;
            _khoHanhDong = khoHanhDong;
            _nhanVien = nhanVien;
            _logger = logger;
        }

        // Gửi một tin nhắn cho trợ lý. Trả JSON để JS vẽ vào khung chat.
        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("ai")]
        public async Task<IActionResult> Hoi([FromBody] HoiTroLyVm vm, CancellationToken ct)
        {
            var maNguoiDung = QuyenNguoiDung.MaNguoiDung(User);
            if (maNguoiDung == null) return Unauthorized();

            var tinNhan = (vm?.TinNhan ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(tinNhan))
                return Json(new TraLoiTroLyVm { ThanhCong = false, Loi = "Vui lòng nhập nội dung cần hỏi." });
            if (tinNhan.Length > 2000)
                return Json(new TraLoiTroLyVm { ThanhCong = false, Loi = "Tin nhắn quá dài (tối đa 2000 ký tự)." });

            var lichSu = (vm!.LichSu ?? new List<LuotChatVm>())
                .Where(x => !string.IsNullOrWhiteSpace(x.NoiDung) && x.NoiDung.Length <= 4000)
                .Select(x => new LuotHoiThoai
                {
                    VaiTro = x.VaiTro == "tro_ly" ? "tro_ly" : "nguoi_dung",
                    NoiDung = x.NoiDung.Trim()
                });

            try
            {
                var kq = await _troLy.HoiAsync(maNguoiDung.Value, tinNhan, lichSu, ct);
                if (!kq.ThanhCong)
                    return Json(new TraLoiTroLyVm { ThanhCong = false, Loi = kq.Loi });

                return Json(new TraLoiTroLyVm
                {
                    ThanhCong = true,
                    TraLoi = kq.TraLoi,
                    HanhDong = ChuyenThe(kq.HanhDong)
                });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Người dùng đóng trang giữa chừng, không cần báo lỗi.
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                // Ghi log chi tiết ở server nhưng không trả nội dung kỹ thuật ra trình duyệt.
                _logger.LogError(ex, "Lỗi khi gọi trợ lý AI cho người dùng {MaNguoiDung}", maNguoiDung);
                return Json(new TraLoiTroLyVm
                {
                    ThanhCong = false,
                    Loi = "Trợ lý đang gặp sự cố. Vui lòng thử lại sau ít phút."
                });
            }
        }

        // Người dùng bấm "Xác nhận": chỉ lúc này dữ liệu mới được ghi.
        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("ai")]
        public async Task<IActionResult> XacNhan([FromBody] XacNhanHanhDongVm vm, CancellationToken ct)
        {
            var maNguoiDung = QuyenNguoiDung.MaNguoiDung(User);
            if (maNguoiDung == null) return Unauthorized();

            var hanhDong = _khoHanhDong.Lay(maNguoiDung.Value, vm?.Id ?? string.Empty);
            if (hanhDong == null)
            {
                return Json(new TraLoiTroLyVm
                {
                    ThanhCong = false,
                    Loi = "Bản nháp này đã hết hạn hoặc không còn hiệu lực. Vui lòng yêu cầu lại."
                });
            }

            if (hanhDong.Loai != "tao_nhan_vien" || hanhDong.NhanVien == null)
            {
                _khoHanhDong.Xoa(maNguoiDung.Value, hanhDong.Id);
                return Json(new TraLoiTroLyVm { ThanhCong = false, Loi = "Loại thao tác này chưa được hỗ trợ." });
            }

            // Xoá trước khi ghi để một bản nháp không thể được xác nhận hai lần (kể cả khi bấm nhanh 2 lần).
            _khoHanhDong.Xoa(maNguoiDung.Value, hanhDong.Id);

            try
            {
                var kq = await _nhanVien.TaoAsync(hanhDong.NhanVien, ct);
                if (!kq.ThanhCong)
                {
                    var loi = string.Join(" ", kq.Loi.Select(x => x.ThongBao));
                    return Json(new TraLoiTroLyVm
                    {
                        ThanhCong = false,
                        Loi = string.IsNullOrWhiteSpace(loi) ? "Không tạo được nhân viên." : loi
                    });
                }

                _logger.LogInformation("Trợ lý AI tạo nhân viên {TenDangNhap} (người xác nhận {MaNguoiDung})",
                    kq.TenDangNhap, maNguoiDung);

                return Json(new TraLoiTroLyVm
                {
                    ThanhCong = true,
                    TraLoi = $"Đã tạo nhân viên {hanhDong.NhanVien.HoTen} với tài khoản {kq.TenDangNhap}. "
                        + "Mật khẩu tạm chỉ hiện đúng một lần — hãy sao chép và gửi cho nhân viên. "
                        + "Họ sẽ phải đổi mật khẩu ở lần đăng nhập đầu tiên.",
                    TenDangNhapMoi = kq.TenDangNhap,
                    MatKhauTam = hanhDong.NhanVien.MatKhau
                });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xác nhận tạo nhân viên qua trợ lý AI");
                return Json(new TraLoiTroLyVm
                {
                    ThanhCong = false,
                    Loi = "Không tạo được nhân viên do lỗi hệ thống. Vui lòng thử lại."
                });
            }
        }

        // Huỷ bản nháp: xoá khỏi bộ nhớ đệm, không ghi gì cả.
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Huy([FromBody] XacNhanHanhDongVm vm)
        {
            var maNguoiDung = QuyenNguoiDung.MaNguoiDung(User);
            if (maNguoiDung == null) return Unauthorized();

            _khoHanhDong.Xoa(maNguoiDung.Value, vm?.Id ?? string.Empty);
            return Json(new TraLoiTroLyVm { ThanhCong = true, TraLoi = "Đã huỷ bản nháp." });
        }

        private static TheHanhDongVm? ChuyenThe(HanhDongCho? hanhDong)
        {
            if (hanhDong == null) return null;
            return new TheHanhDongVm
            {
                Id = hanhDong.Id,
                TieuDe = hanhDong.TieuDe,
                TomTat = hanhDong.TomTat
                    .Select(x => new DongTomTatVm { Nhan = x.Key, GiaTri = x.Value })
                    .ToList(),
                HetHanLuc = hanhDong.HetHan.ToString("HH:mm")
            };
        }
    }
}
