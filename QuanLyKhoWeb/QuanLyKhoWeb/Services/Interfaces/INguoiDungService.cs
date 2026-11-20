using QuanLyKhoWeb.Models.Entities;

namespace QuanLyKhoWeb.Services.Interfaces
{
    public interface INguoiDungService
    {
        Task<NguoiDung?> XacThucAsync(string tenDangNhap, string matKhau);
    }
}
