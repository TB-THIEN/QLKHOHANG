# QuanLyKhoWeb - Web quản lý kho (ASP.NET Core MVC)

Ứng dụng quản lý kho xây dựng bằng ASP.NET Core MVC (.NET 10), EF Core + Dapper, SQL Server,
xác thực bằng cookie, phân quyền theo vai trò và gửi OTP qua SMTP để đặt lại mật khẩu.

## Cấu trúc

```
.
├── database/
│   └── Do_An_Kho.sql          # Script tạo schema + dữ liệu mẫu
├── QuanLyKhoWeb/
│   ├── QuanLyKhoWeb.slnx
│   └── QuanLyKhoWeb/          # Mã nguồn chính
│       ├── Controllers/
│       ├── Services/
│       ├── Models/
│       ├── Views/
│       ├── Middlewares/
│       └── wwwroot/
└── README.md
```

## Yêu cầu

- .NET SDK 10.0
- SQL Server (mặc định `localhost\SQLEXPRESS`, xác thực Windows)

## Cài đặt

1. Tạo cơ sở dữ liệu:

   ```powershell
   sqlcmd -S .\SQLEXPRESS -E -i .\database\Do_An_Kho.sql
   ```

   Script sẽ tạo database `DO_AN_QLK`, schema và một tài khoản quản lý mẫu.

2. Cấu hình kết nối (nếu khác mặc định): sửa `ConnectionStrings:DefaultConnection`
   trong `appsettings.json`, hoặc dùng user-secrets:

   ```powershell
   cd QuanLyKhoWeb\QuanLyKhoWeb
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."
   ```

3. Cấu hình SMTP để dùng chức năng quên mật khẩu (tuỳ chọn). **Không ghi mật khẩu
   vào `appsettings.json`**:

   ```powershell
   dotnet user-secrets set "Smtp:User" "your@gmail.com"
   dotnet user-secrets set "Smtp:Pass" "<Gmail App Password 16 ký tự>"
   dotnet user-secrets set "Smtp:From" "your@gmail.com"
   ```

4. Chạy:

   ```powershell
   dotnet run --project .\QuanLyKhoWeb\QuanLyKhoWeb
   ```

   Mở `https://localhost:7151` (hoặc `http://localhost:5213`).

## Tài khoản mặc định

Script seed tạo tài khoản `admin` với mật khẩu mặc định `Admin@123` và cờ
`phai_doi_mat_khau = 1` (bắt buộc đổi ở lần đăng nhập đầu). **Hãy đổi mật khẩu ngay
sau khi triển khai**, hoặc xoá phần seed tài khoản trong script trước khi dùng thật.

## Bảo mật

- Không commit `appsettings` chứa bí mật, `.env`, chứng chỉ, dump CSDL (`*.bak`, `*.mdf`) —
  đã khai báo trong `.gitignore`.
- Không commit ảnh thẻ/CCCD nhân viên trong `wwwroot/images/staff/` (dữ liệu cá nhân).
- Mật khẩu và OTP được băm bằng BCrypt; toàn bộ POST dùng `ValidateAntiForgeryToken`.
- Giới hạn tần suất: đăng nhập 10 lần/phút/IP và khóa tài khoản 5 phút sau 5 lần sai;
  gửi OTP 5 lần/5 phút/IP, tối đa 5 mã/giờ/tài khoản; nhập sai OTP 5 lần thì mã bị hủy.
