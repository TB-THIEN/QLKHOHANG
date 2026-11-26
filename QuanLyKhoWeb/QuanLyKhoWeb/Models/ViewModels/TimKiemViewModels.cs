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

    // Thanh phân trang dùng chung: giữ nguyên bộ lọc hiện tại khi chuyển trang.
    public class PhanTrangVm
    {
        public int Trang { get; set; } = 1;
        public int TotalPage { get; set; } = 1;
        public int Total { get; set; } = 0;
        public int PageSize { get; set; } = 20;
        public string Nhan { get; set; } = "bản ghi";   // "sản phẩm", "đơn hàng"…
        public string? Controller { get; set; }          // null = controller hiện tại
        public string Action { get; set; } = "Index";
        public string AriaLabel { get; set; } = "Phân trang";
        // Giữ bộ lọc/từ khóa khi bấm sang trang khác
        public Dictionary<string, string?> ThamSo { get; set; } = new();
    }
}
