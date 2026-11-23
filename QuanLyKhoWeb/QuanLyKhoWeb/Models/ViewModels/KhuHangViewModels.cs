namespace QuanLyKhoWeb.Models.ViewModels
{
    public class KhuHangIndexVm
    {
        public string? Search { get; set; }

        public List<QuanLyKhoWeb.Models.Entities.KhuVucKho> Khus { get; set; } = new();

        /// <summary>
        /// Toàn bộ khu (kèm kệ) dùng cho hộp thoại đổi khu/kệ. Không lọc theo từ khóa
        /// để luôn chọn được mọi vị trí đích.
        /// </summary>
        public List<QuanLyKhoWeb.Models.Entities.KhuVucKho> KhuChonDich { get; set; } = new();

        public List<HangTimKiemVm> KetQua { get; set; } = new();
    }

    public class HangTimKiemVm
    {
        public int MaSku { get; set; }

        public string TenSanPham { get; set; } = string.Empty;

        public string MaCodeSku { get; set; } = string.Empty;

        public string? HinhAnh { get; set; }

        public decimal GiaTien { get; set; }

        public int SoLuongTon { get; set; }

        public List<string> ViTri { get; set; } = new();
    }
}
