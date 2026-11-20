using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Services.Interfaces;
using System.Globalization;
using System.Text.Json.Nodes;

namespace QuanLyKhoWeb.Services.Ai
{
    // Kết quả một tool trả về cho model: dữ liệu dạng JSON + có cần dừng vòng lặp không.
    public class KetQuaCongCu
    {
        public JsonObject DuLieu { get; init; } = new();
        public HanhDongCho? HanhDong { get; init; }

        // true khi đã tạo xong "hành động chờ": dừng gọi thêm tool, chờ người dùng xác nhận.
        public bool DaSanSangXacNhan => HanhDong != null;
    }

    // Danh sách công cụ trợ lý được phép dùng. Chỉ có 3 tool, tất cả đều đi qua
    // service nghiệp vụ hiện có. Trợ lý KHÔNG có đường nào chạm thẳng vào cơ sở dữ liệu.
    public class KhoCongCu
    {
        public const string LietKeVaiTro = "liet_ke_vai_tro";
        public const string TimNhanVien = "tim_nhan_vien";
        public const string TaoNhanVien = "tao_nhan_vien";

        private readonly DoAnQlkContext _context;
        private readonly INhanVienService _nhanVien;
        private readonly KhoHanhDongCho _khoHanhDong;

        public KhoCongCu(DoAnQlkContext context, INhanVienService nhanVien, KhoHanhDongCho khoHanhDong)
        {
            _context = context;
            _nhanVien = nhanVien;
            _khoHanhDong = khoHanhDong;
        }

        // Khai báo tool cho model (OpenAPI rút gọn mà Gemini chấp nhận).
        public JsonArray KhaiBao() => new()
        {
            new JsonObject
            {
                ["name"] = LietKeVaiTro,
                ["description"] = "Liệt kê các vai trò nhân viên đang có trong hệ thống. Dùng khi cần biết vai trò chính xác trước khi tạo nhân viên.",
                ["parameters"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() }
            },
            new JsonObject
            {
                ["name"] = TimNhanVien,
                ["description"] = "Tìm nhân viên theo họ tên, tên đăng nhập, email, số điện thoại hoặc CCCD. Dùng để kiểm tra trùng trước khi tạo mới.",
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["tu_khoa"] = new JsonObject { ["type"] = "string", ["description"] = "Từ khóa cần tìm" }
                    },
                    ["required"] = new JsonArray("tu_khoa")
                }
            },
            new JsonObject
            {
                ["name"] = TaoNhanVien,
                ["description"] = "Chuẩn bị tạo một nhân viên mới. Công cụ này KHÔNG ghi vào cơ sở dữ liệu ngay: nó kiểm tra dữ liệu và tạo bản nháp để quản lý bấm xác nhận.",
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["ho_ten"] = new JsonObject { ["type"] = "string", ["description"] = "Họ tên đầy đủ, không chứa chữ số" },
                        ["ten_dang_nhap"] = new JsonObject { ["type"] = "string", ["description"] = "Tên đăng nhập duy nhất, không dấu, tối đa 50 ký tự" },
                        ["vai_tro"] = new JsonObject { ["type"] = "string", ["description"] = "Tên vai trò, dùng đúng tên có sẵn nếu phù hợp" },
                        ["email"] = new JsonObject { ["type"] = "string", ["description"] = "Bắt buộc có đuôi @gmail.com nếu nhập" },
                        ["so_dien_thoai"] = new JsonObject { ["type"] = "string" },
                        ["ngay_sinh"] = new JsonObject { ["type"] = "string", ["description"] = "Định dạng YYYY-MM-DD" },
                        ["gioi_tinh"] = new JsonObject { ["type"] = "string", ["description"] = "Nam, Nữ hoặc Khác" },
                        ["thuong_tru"] = new JsonObject { ["type"] = "string" },
                        ["cccd"] = new JsonObject { ["type"] = "string", ["description"] = "Đúng 12 chữ số nếu nhập" },
                        ["stk_ngan_hang"] = new JsonObject { ["type"] = "string" },
                        ["chi_nhanh_ngan_hang"] = new JsonObject { ["type"] = "string" },
                        ["trang_thai_hoat_dong"] = new JsonObject { ["type"] = "boolean", ["description"] = "true = đang làm, false = nghỉ làm" }
                    },
                    ["required"] = new JsonArray("ho_ten", "ten_dang_nhap", "vai_tro")
                }
            }
        };

        public async Task<KetQuaCongCu> ThucThiAsync(
            int maNguoiDung, string ten, JsonObject thamSo, CancellationToken ct = default)
        {
            return ten switch
            {
                LietKeVaiTro => await LietKeVaiTroAsync(ct),
                TimNhanVien => await TimNhanVienAsync(thamSo, ct),
                TaoNhanVien => await TaoNhanVienAsync(maNguoiDung, thamSo, ct),
                _ => new KetQuaCongCu
                {
                    DuLieu = new JsonObject { ["loi"] = "Công cụ không được phép sử dụng." }
                }
            };
        }

        private async Task<KetQuaCongCu> LietKeVaiTroAsync(CancellationToken ct)
        {
            // Ẩn vai trò Quản lý: hệ thống chỉ cho phép đúng 1 quản lý (SP cũng chặn tạo thêm).
            var ds = await _context.VaiTros
                .Where(v => v.TenVaiTro != "Quản lý")
                .OrderBy(v => v.TenVaiTro)
                .Select(v => v.TenVaiTro)
                .ToListAsync(ct);

            return new KetQuaCongCu
            {
                DuLieu = new JsonObject { ["vai_tro"] = new JsonArray(ds.Select(x => (JsonNode)x!).ToArray()) }
            };
        }

        private async Task<KetQuaCongCu> TimNhanVienAsync(JsonObject thamSo, CancellationToken ct)
        {
            var tuKhoa = LayChuoi(thamSo, "tu_khoa")?.Trim();
            if (string.IsNullOrWhiteSpace(tuKhoa))
                return new KetQuaCongCu { DuLieu = new JsonObject { ["loi"] = "Thiếu từ khóa tìm kiếm." } };

            var ds = await _context.NguoiDungs
                .Where(n => n.HoTen.Contains(tuKhoa)
                    || n.TenDangNhap.Contains(tuKhoa)
                    || (n.Email != null && n.Email.Contains(tuKhoa))
                    || (n.SoDienThoai != null && n.SoDienThoai.Contains(tuKhoa))
                    || (n.Cccd != null && n.Cccd.Contains(tuKhoa)))
                .OrderBy(n => n.MaNguoiDung)
                .Take(10)
                .Select(n => new
                {
                    n.MaNguoiDung,
                    n.TenDangNhap,
                    n.HoTen,
                    n.Email,
                    n.SoDienThoai,
                    VaiTro = n.MaVaiTroNavigation.TenVaiTro,
                    DangLam = n.TrangThaiHoatDong == true
                })
                .ToListAsync(ct);

            var mang = new JsonArray();
            foreach (var n in ds)
            {
                mang.Add(new JsonObject
                {
                    ["ma_nguoi_dung"] = n.MaNguoiDung,
                    ["ho_ten"] = n.HoTen,
                    ["ten_dang_nhap"] = n.TenDangNhap,
                    ["vai_tro"] = n.VaiTro,
                    ["email"] = n.Email,
                    ["so_dien_thoai"] = n.SoDienThoai,
                    ["dang_lam"] = n.DangLam
                });
            }

            return new KetQuaCongCu
            {
                DuLieu = new JsonObject { ["so_ket_qua"] = mang.Count, ["nhan_vien"] = mang }
            };
        }

        private async Task<KetQuaCongCu> TaoNhanVienAsync(int maNguoiDung, JsonObject thamSo, CancellationToken ct)
        {
            var hoTen = LayChuoi(thamSo, "ho_ten");
            var tenDangNhap = LayChuoi(thamSo, "ten_dang_nhap");
            var vaiTro = LayChuoi(thamSo, "vai_tro");

            // Model không được tự đặt mật khẩu: hệ thống sinh mật khẩu tạm và buộc đổi lần đầu.
            var matKhauTam = NhanVienService.SinhMatKhauTam();

            var yeuCau = new NhanVienTaoMoi
            {
                HoTen = hoTen?.Trim() ?? string.Empty,
                TenDangNhap = tenDangNhap?.Trim() ?? string.Empty,
                TenVaiTro = vaiTro?.Trim() ?? string.Empty,
                MatKhau = matKhauTam,
                Email = LayChuoi(thamSo, "email"),
                SoDienThoai = LayChuoi(thamSo, "so_dien_thoai"),
                NgaySinh = LayNgay(thamSo, "ngay_sinh"),
                GioiTinh = LayChuoi(thamSo, "gioi_tinh"),
                ThuongTru = LayChuoi(thamSo, "thuong_tru"),
                Cccd = LayChuoi(thamSo, "cccd"),
                StkNganHang = LayChuoi(thamSo, "stk_ngan_hang"),
                ChiNhanhNganHang = LayChuoi(thamSo, "chi_nhanh_ngan_hang"),
                TrangThaiHoatDong = LayBool(thamSo, "trang_thai_hoat_dong") ?? true
            };

            var loi = await _nhanVien.KiemTraAsync(yeuCau, ct);
            if (loi.Count > 0)
            {
                var mang = new JsonArray();
                foreach (var l in loi)
                    mang.Add(new JsonObject { ["truong"] = l.Truong, ["thong_bao"] = l.ThongBao });

                return new KetQuaCongCu
                {
                    DuLieu = new JsonObject
                    {
                        ["ket_qua"] = "DU_LIEU_CHUA_HOP_LE",
                        ["loi"] = mang,
                        ["huong_dan"] = "Hỏi lại người dùng để sửa các trường bị lỗi. Không được tự bịa thông tin."
                    }
                };
            }

            var tomTat = new List<KeyValuePair<string, string>>
            {
                new("Họ tên", yeuCau.HoTen),
                new("Tên đăng nhập", yeuCau.TenDangNhap),
                new("Vai trò", yeuCau.TenVaiTro),
                new("Trạng thái", yeuCau.TrangThaiHoatDong ? "Đang làm" : "Nghỉ làm")
            };
            if (!string.IsNullOrWhiteSpace(yeuCau.Email)) tomTat.Add(new("Email", yeuCau.Email!));
            if (!string.IsNullOrWhiteSpace(yeuCau.SoDienThoai)) tomTat.Add(new("Điện thoại", yeuCau.SoDienThoai!));
            if (!string.IsNullOrWhiteSpace(yeuCau.Cccd)) tomTat.Add(new("CCCD", yeuCau.Cccd!));
            if (yeuCau.NgaySinh.HasValue) tomTat.Add(new("Ngày sinh", yeuCau.NgaySinh.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
            if (!string.IsNullOrWhiteSpace(yeuCau.GioiTinh)) tomTat.Add(new("Giới tính", yeuCau.GioiTinh!));
            if (!string.IsNullOrWhiteSpace(yeuCau.ThuongTru)) tomTat.Add(new("Thường trú", yeuCau.ThuongTru!));
            if (!string.IsNullOrWhiteSpace(yeuCau.StkNganHang)) tomTat.Add(new("STK ngân hàng", yeuCau.StkNganHang!));
            if (!string.IsNullOrWhiteSpace(yeuCau.ChiNhanhNganHang)) tomTat.Add(new("Chi nhánh NH", yeuCau.ChiNhanhNganHang!));

            var hanhDong = _khoHanhDong.Them(
                maNguoiDung,
                "tao_nhan_vien",
                $"Thêm nhân viên {yeuCau.HoTen}",
                yeuCau,
                tomTat);

            return new KetQuaCongCu
            {
                HanhDong = hanhDong,
                DuLieu = new JsonObject
                {
                    ["ket_qua"] = "DA_CHUAN_BI_BAN_NHAP",
                    ["ghi_chu"] = "Bản nháp đã hiện cho người dùng. Chưa có gì được ghi vào hệ thống. "
                        + "Hãy tóm tắt ngắn và nhắc người dùng bấm Xác nhận.",
                    ["ma_hanh_dong"] = hanhDong.Id
                }
            };
        }

        private static string? LayChuoi(JsonObject o, string ten)
        {
            var node = o[ten];
            if (node == null) return null;
            try
            {
                var s = node.GetValue<string>();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            catch (Exception)
            {
                return node.ToJsonString().Trim('"');
            }
        }

        private static bool? LayBool(JsonObject o, string ten)
        {
            var node = o[ten];
            if (node == null) return null;
            try { return node.GetValue<bool>(); }
            catch (Exception)
            {
                var s = node.ToJsonString().Trim('"');
                if (bool.TryParse(s, out var b)) return b;
                if (s is "1" or "co" or "có") return true;
                if (s is "0" or "khong" or "không") return false;
                return null;
            }
        }

        private static DateOnly? LayNgay(JsonObject o, string ten)
        {
            var s = LayChuoi(o, ten);
            if (string.IsNullOrWhiteSpace(s)) return null;
            return DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d
                : null;
        }
    }
}
