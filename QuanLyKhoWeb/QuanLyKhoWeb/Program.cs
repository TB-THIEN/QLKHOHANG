using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.RateLimiting;
using QuanLyKhoWeb.Authorization;
using QuanLyKhoWeb.Data;
using QuanLyKhoWeb.Services;
using QuanLyKhoWeb.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Yêu cầu đăng nhập mặc định cho MỌI controller/action, trừ chỗ đánh dấu [AllowAnonymous]
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter(
        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
});

builder.Services.AddDbContext<DoAnQlkContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<QuanLyKhoWeb.Services.Interfaces.IDbConnectionFactory>(_ =>
    new SqlConnectionFactory(builder.Configuration.GetConnectionString("DefaultConnection")!));

builder.Services.AddScoped<INguoiDungService, NguoiDungService>();
builder.Services.AddScoped<IDonHangService, DonHangService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Chống dò mật khẩu / spam OTP.
// - Giới hạn theo IP ở tầng middleware (dưới đây)
// - Khóa theo tài khoản sau 5 lần sai (BaoVeDangNhap, dùng trong AccountController)
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<BaoVeDangNhap>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Đăng nhập: 10 lần / phút / IP
    options.AddPolicy("dangnhap", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(LayIp(httpContext), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // Gửi mã OTP: 5 lần / 5 phút / IP
    options.AddPolicy("gui-otp", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(LayIp(httpContext), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        }));

    // Xác nhận OTP + đổi mật khẩu: 10 lần / 5 phút / IP
    options.AddPolicy("xac-nhan-otp", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(LayIp(httpContext), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        }));

    options.OnRejected = async (context, token) =>
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;

        var giayCho = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var cho)
            ? Math.Max(1, (int)Math.Ceiling(cho.TotalSeconds))
            : 60;
        response.Headers.RetryAfter = giayCho.ToString();

        // Nút "Gửi mã" gọi bằng fetch() nên phải trả JSON; các form khác trả trang thông báo
        if (context.HttpContext.Request.Path.StartsWithSegments("/Account/GuiMa"))
        {
            response.ContentType = "application/json; charset=utf-8";
            await response.WriteAsync(JsonSerializer.Serialize(new
            {
                thanhCong = false,
                thongBao = $"Bạn thao tác quá nhanh. Vui lòng thử lại sau {giayCho} giây."
            }), token);
            return;
        }

        var quayLai = context.HttpContext.Request.Path.StartsWithSegments("/Account/QuenMatKhau")
            ? "/Account/QuenMatKhau"
            : "/Account/DangNhap";
        response.ContentType = "text/html; charset=utf-8";
        await response.WriteAsync(TrangQuaNhieu(quayLai, giayCho), token);
    };
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/DangNhap";
        options.AccessDeniedPath = "/Account/TuChoiTruyCap";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    // Mỗi nhóm trang một policy riêng, cùng gọi về Authorization/QuyenNguoiDung
    // để controller và view luôn khớp nhau.
    void ThemPolicy(string ten, Func<System.Security.Claims.ClaimsPrincipal, bool> kiemTra)
        => options.AddPolicy(ten, policy => policy.RequireAssertion(ctx => kiemTra(ctx.User)));

    // Danh sách nhân viên: chỉ Quản lý
    options.AddPolicy("ChiQuanLy", policy => policy.RequireClaim("VaiTro", QuyenNguoiDung.QuanLy));
    // Báo cáo: Quản lý + Nhân viên bán hàng
    ThemPolicy("XemBaoCao", QuyenNguoiDung.XemBaoCao);
    // Đơn hàng: Quản lý + Nhân viên bán hàng được tạo/hủy
    ThemPolicy("SuaDonHang", QuyenNguoiDung.SuaDonHang);
    // Khuyến mãi: Quản lý + Nhân viên bán hàng
    ThemPolicy("SuaKhuyenMai", QuyenNguoiDung.SuaKhuyenMai);
    // Nhà cung cấp: chỉ Quản lý
    ThemPolicy("SuaNhaCungCap", QuyenNguoiDung.SuaNhaCungCap);
    // Sản phẩm / Khu hàng: Quản lý + Nhân viên kho + Nhân viên bán hàng
    ThemPolicy("SuaSanPham", QuyenNguoiDung.SuaSanPham);
    ThemPolicy("SuaKhuHang", QuyenNguoiDung.SuaKhuHang);
    // Nhập kho (phiếu nhập, quét mã): Quản lý + Nhân viên kho
    ThemPolicy("DuocNhapKho", QuyenNguoiDung.NhapKho);
    // Vận chuyển: gán nhân viên giao / xóa vận đơn
    ThemPolicy("SuaVanChuyen", QuyenNguoiDung.SuaVanChuyen);
    // Xác nhận đã giao: Quản lý + Nhân viên bán hàng + Nhân viên giao hàng
    ThemPolicy("DuocXacNhanGiao", QuyenNguoiDung.XacNhanGiao);
    // Hủy đơn: theo cột co_quyen_huy_don của vai trò
    ThemPolicy("DuocHuyDon", QuyenNguoiDung.HuyDon);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Gắn header bảo mật (CSP, nosniff, chống nhúng frame...) cho mọi response.
app.UseMiddleware<QuanLyKhoWeb.Middlewares.SecurityHeadersMiddleware>();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Bắt buộc đổi mật khẩu lần đầu trước khi dùng mọi chức năng khác.
// Đặt sau UseStaticFiles nên file tĩnh không bị chặn.
app.UseMiddleware<QuanLyKhoWeb.Middlewares.BatBuocDoiMatKhauMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=DangNhap}/{id?}");

app.Run();

// IP dùng để chia "xô" giới hạn request. Khi chạy sau reverse proxy/load balancer
// cần bật ForwardedHeaders để lấy đúng IP người dùng.
static string LayIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "khong-ro-ip";

static string TrangQuaNhieu(string quayLai, int giayCho) => $"""
    <!DOCTYPE html>
    <html lang="vi">
    <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Quá nhiều yêu cầu</title>
    </head>
    <body style="font-family:system-ui,'Segoe UI',sans-serif;max-width:520px;margin:80px auto;padding:0 20px;line-height:1.6;color:#1f2933">
        <h1 style="font-size:20px;margin-bottom:8px">Bạn thao tác quá nhiều lần</h1>
        <p>Vì lý do an toàn, vui lòng thử lại sau <b>{giayCho} giây</b>.</p>
        <p><a href="{quayLai}" style="color:#0b6bcb">Quay lại</a></p>
    </body>
    </html>
    """;
