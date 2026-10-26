using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
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
    // Chỉ vai trò "Quản lý" mới xem được Danh sách nhân viên
    options.AddPolicy("ChiQuanLy", policy => policy.RequireClaim("VaiTro", "Quản lý"));
    // Được tạo/sửa dữ liệu: Quản lý + Nhân viên kho + Nhân viên bán hàng.
    // Chặn đúng Nhân viên giao hàng (thay vì liệt kê tên cho phép) để các vai trò
    // tự tạo thêm sau này vẫn làm việc bình thường.
    options.AddPolicy("DuocTao", policy => policy.RequireAssertion(ctx =>
        ctx.User.FindFirst("VaiTro")?.Value != "Nhân viên giao hàng"));
    // Xác nhận vận đơn "Đã giao": Quản lý + Nhân viên giao hàng + Nhân viên bán hàng.
    options.AddPolicy("DuocXacNhanGiao", policy => policy.RequireAssertion(ctx =>
    {
        var vaiTro = ctx.User.FindFirst("VaiTro")?.Value;
        return vaiTro == "Quản lý" || vaiTro == "Nhân viên giao hàng" || vaiTro == "Nhân viên bán hàng";
    }));
    // Nhập kho: Quản lý + Nhân viên kho (tạo/hoàn tất/hủy phiếu nhập).
    options.AddPolicy("DuocNhapKho", policy => policy.RequireAssertion(ctx =>
    {
        var vaiTro = ctx.User.FindFirst("VaiTro")?.Value;
        return vaiTro == "Quản lý" || vaiTro == "Nhân viên kho";
    }));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Bắt buộc đổi mật khẩu lần đầu trước khi dùng mọi chức năng khác.
// Đặt sau UseStaticFiles nên file tĩnh không bị chặn.
app.UseMiddleware<QuanLyKhoWeb.Middlewares.BatBuocDoiMatKhauMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=DangNhap}/{id?}");

app.Run();