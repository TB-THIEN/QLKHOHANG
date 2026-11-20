namespace QuanLyKhoWeb.Models.ViewModels
{
    // Một lượt trong khung chat, do trình duyệt gửi kèm để trợ lý hiểu ngữ cảnh.
    public class LuotChatVm
    {
        public string VaiTro { get; set; } = "nguoi_dung";
        public string NoiDung { get; set; } = string.Empty;
    }

    public class HoiTroLyVm
    {
        public string TinNhan { get; set; } = string.Empty;
        public List<LuotChatVm> LichSu { get; set; } = new();
    }

    public class XacNhanHanhDongVm
    {
        public string Id { get; set; } = string.Empty;
    }

    // Phản hồi cho trình duyệt. Dùng chung một hình dạng cho mọi endpoint của trợ lý.
    public class TraLoiTroLyVm
    {
        public bool ThanhCong { get; set; }
        public string? TraLoi { get; set; }
        public string? Loi { get; set; }
        public TheHanhDongVm? HanhDong { get; set; }

        // Mật khẩu tạm chỉ trả về đúng một lần ngay sau khi tạo thành công.
        public string? MatKhauTam { get; set; }
        public string? TenDangNhapMoi { get; set; }
    }

    public class TheHanhDongVm
    {
        public string Id { get; set; } = string.Empty;
        public string TieuDe { get; set; } = string.Empty;
        public List<DongTomTatVm> TomTat { get; set; } = new();
        public string? HetHanLuc { get; set; }
    }

    public class DongTomTatVm
    {
        public string Nhan { get; set; } = string.Empty;
        public string GiaTri { get; set; } = string.Empty;
    }
}
