using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyKhoWeb.Data;

namespace QuanLyKhoWeb.Middlewares
{
    // Chặn mọi chức năng khi tài khoản còn cờ PhaiDoiMatKhau (đăng nhập lần đầu
    // hoặc vừa bị quản lý đặt lại MK): chỉ được ở trang đổi mật khẩu hoặc đăng xuất.
    // Kiểm tra trực tiếp DB mỗi request (1 truy vấn theo khóa chính) để luôn đúng
    // kể cả khi cờ vừa bị bật lại giữa phiên đăng nhập.
    public class BatBuocDoiMatKhauMiddleware
    {
        private readonly RequestDelegate _next;
        public BatBuocDoiMatKhauMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";
            if (context.User.Identity?.IsAuthenticated == true && !DuocMien(path))
            {
                var maNd = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(maNd, out var id))
                {
                    var db = context.RequestServices.GetRequiredService<DoAnQlkContext>();
                    var phaiDoi = await db.NguoiDungs
                        .Where(n => n.MaNguoiDung == id)
                        .Select(n => n.PhaiDoiMatKhau)
                        .FirstOrDefaultAsync();
                    if (phaiDoi)
                    {
                        context.Response.Redirect("/NhanVien/DoiMatKhau");
                        return;
                    }
                }
            }
            await _next(context);
        }

        // Các đường được phép khi chưa đổi MK: trang đổi MK, đăng xuất/nhập,
        // quên MK, từ chối truy cập, trang lỗi và file tĩnh (do UseStaticFiles phục vụ trước).
        private static bool DuocMien(string path)
        {
            return path.StartsWith("/NhanVien/DoiMatKhau", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Account/DangNhap", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Account/DangXuat", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Account/QuenMatKhau", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Account/GuiMa", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Account/TuChoiTruyCap", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/Home/Error", StringComparison.OrdinalIgnoreCase);
        }
    }
}
