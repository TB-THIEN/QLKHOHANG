namespace QuanLyKhoWeb.Models.ViewModels
{
    // Ô tìm kiếm riêng của từng trang: lọc danh sách + gợi ý ngay khi gõ.
    public class OTimKiemVm
    {
        public string Trang { get; set; } = string.Empty;          // khóa trang để API trả đúng bộ gợi ý
        public string Controller { get; set; } = string.Empty;     // nơi nhận lọc
        public string Action { get; set; } = "Index";
        public string? GiaTri { get; set; }                        // từ khóa đang lọc
        public string Placeholder { get; set; } = "Tìm kiếm…";
        // Giữ các bộ lọc khác khi submit ô tìm kiếm
        public Dictionary<string, string?> ThamSoThem { get; set; } = new();
    }

    public class GoiYTimKiemVm
    {
        public string Nhom { get; set; } = string.Empty;

        public string TieuDe { get; set; } = string.Empty;

        public string? MoTa { get; set; }

        public string Url { get; set; } = string.Empty;

        public string Icon { get; set; } = "bi-search";
    }
}
