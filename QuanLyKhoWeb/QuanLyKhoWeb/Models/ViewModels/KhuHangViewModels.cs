namespace QuanLyKhoWeb.Models.ViewModels
{
    public class KhuHangIndexVm
    {
        public string? Search { get; set; }

        public List<QuanLyKhoWeb.Models.Entities.KhuVucKho> Khus { get; set; } = new();

        public List<HangTimKiemVm> KetQua { get; set; } = new();
    }

    public class HangTimKiemVm
    {
        public int MaSku { get; set; }

        public string TenSanPham { get; set; } = string.Empty;

        public string MaCodeSku { get; set; } = string.Empty;

        public string? HinhAnh { get; set; }

        public decimal GiaNiemYet { get; set; }

        public int SoLuongTon { get; set; }

        public List<string> ViTri { get; set; } = new();
    }
}
