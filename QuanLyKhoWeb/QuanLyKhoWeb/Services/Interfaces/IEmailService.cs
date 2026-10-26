namespace QuanLyKhoWeb.Services.Interfaces
{
    public interface IEmailService
    {
        Task GuiMaXacNhanAsync(string toiEmail, string otp);
    }
}
