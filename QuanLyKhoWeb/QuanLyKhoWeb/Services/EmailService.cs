using System.Net;
using System.Net.Mail;
using QuanLyKhoWeb.Services.Interfaces;

namespace QuanLyKhoWeb.Services
{
    // Gửi mail thật qua SMTP (mặc định cấu hình Gmail). Không cần gói ngoài, dùng SmtpClient có sẵn.
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _cfg;
        public EmailService(IConfiguration cfg) => _cfg = cfg;

        public async Task GuiMaXacNhanAsync(string toiEmail, string otp)
        {
            var host = _cfg["Smtp:Host"] ?? "smtp.gmail.com";
            var port = int.TryParse(_cfg["Smtp:Port"], out var p) ? p : 587;
            var user = _cfg["Smtp:User"];
            var pass = _cfg["Smtp:Pass"];
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
                throw new InvalidOperationException("Chưa cấu hình email gửi (Smtp:User/Pass trong appsettings.json).");

            using var smtp = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(user, pass),
                EnableSsl = true
            };
            var from = _cfg["Smtp:From"] ?? user;
            using var mail = new MailMessage(from, toiEmail,
                "[Kho Hàng] Mã xác nhận đặt lại mật khẩu",
                $"Xin chào,\n\nMã xác nhận đặt lại mật khẩu của bạn là: {otp}\n" +
                $"Mã có hiệu lực 3 phút và chỉ dùng được 1 lần.\n" +
                $"Nếu không phải bạn yêu cầu, vui lòng bỏ qua email này.\n\nTrân trọng,\nKho Hàng");
            await smtp.SendMailAsync(mail);
        }
    }
}
