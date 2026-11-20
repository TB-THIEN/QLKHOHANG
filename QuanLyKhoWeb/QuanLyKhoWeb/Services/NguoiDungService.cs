using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Models.Entities;
using QuanLyKhoWeb.Services.Interfaces;

namespace QuanLyKhoWeb.Services
{
    public class NguoiDungService : INguoiDungService
    {
        private readonly DoAnQlkContext _context;
        public NguoiDungService(DoAnQlkContext context) => _context = context;

        public async Task<NguoiDung?> XacThucAsync(string tenDangNhap, string matKhau)
        {
            var nd = await _context.NguoiDungs
                .Include(x => x.MaVaiTroNavigation)
                .FirstOrDefaultAsync(x => x.TenDangNhap == tenDangNhap && x.TrangThaiHoatDong == true);

            if (nd == null) return null;
            return BCrypt.Net.BCrypt.Verify(matKhau, nd.MatKhauMaHoa) ? nd : null;
        }
    }
}
