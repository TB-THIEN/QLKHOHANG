using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Services;

namespace QuanLyKhoWeb.Services
{
    // Đọc cấu hình cửa hàng (1 dòng duy nhất) cho trang in hóa đơn / phiếu giao hàng.
    // Nếu bảng trống thì trả bản mặc định để trang in không bị vỡ.
    public static class CauHinhCuaHangService
    {
        public static async Task<QuanLyKhoWeb.Models.Entities.CauHinhCuaHang> LayAsync(DoAnQlkContext context)
            => await context.CauHinhCuaHangs.AsNoTracking()
                   .FirstOrDefaultAsync(x => x.MaCauHinh == 1)
               ?? new QuanLyKhoWeb.Models.Entities.CauHinhCuaHang { MaCauHinh = 1, TenCuaHang = "Sổ Kho" };
    }
}
