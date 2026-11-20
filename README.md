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

4. Cấu hình trợ lý AI (tuỳ chọn). Lấy khóa ở [Google AI Studio](https://aistudio.google.com/apikey),
   **không ghi vào `appsettings.json`**:

   ```powershell
   dotnet user-secrets set "Ai:ApiKey" "<khóa API>"
   ```

   Model mặc định là `gemini-3.5-flash-lite`; đổi bằng `Ai:Model` nếu tài khoản của bạn
   dùng model khác. Không có khóa thì khung chat vẫn hiện nhưng báo "chưa cấu hình".

5. Chạy:

   ```powershell
   dotnet run --project .\QuanLyKhoWeb\QuanLyKhoWeb
   ```

   Mở `https://localhost:7151` (hoặc `http://localhost:5213`).

   > Cấu hình đọc từ user-secrets chỉ có tác dụng khi `ASPNETCORE_ENVIRONMENT=Development`.
   > Nếu chạy bằng `dotnet run` không kèm profile, hãy đặt biến môi trường này trước.

## Trợ lý AI

Khung chat nổi ở góc phải dưới, **chỉ Quản lý** thấy. Trợ lý hiện hỗ trợ thêm nhân viên
và tra cứu nhân viên.

Cách hoạt động: model không ghi thẳng vào cơ sở dữ liệu. Khi đủ dữ liệu, nó gọi công cụ
`tao_nhan_vien`, công cụ này chỉ kiểm tra hợp lệ rồi tạo **bản nháp** trong bộ nhớ đệm
(hết hạn sau 10 phút, gắn với tài khoản đang đăng nhập). Quản lý phải bấm **Xác nhận**
thì mới thực sự gọi `sp_TaoNhanVien`.

- Mật khẩu do hệ thống sinh ngẫu nhiên và chỉ hiện **một lần** ngay sau khi tạo; nhân viên
  buộc đổi ở lần đăng nhập đầu (`phai_doi_mat_khau = 1`).
- Model chỉ được dùng 3 công cụ cố định: `liet_ke_vai_tro`, `tim_nhan_vien`, `tao_nhan_vien`.
- Giới hạn 20 lượt / 5 phút / tài khoản (`Program.cs`, policy `"ai"`).
- Quy tắc kiểm tra dữ liệu nằm ở `Services/NhanVienService.cs`, dùng chung cho cả form
  Thêm nhân viên và trợ lý AI.

Mã liên quan: `Services/Ai/`, `Controllers/TroLyController.cs`, `Views/Shared/_TroLyAi.cshtml`,
`wwwroot/js/tro-ly-ai.js`, `wwwroot/css/tro-ly-ai.css`.

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
