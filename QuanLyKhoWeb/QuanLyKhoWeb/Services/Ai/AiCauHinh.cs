namespace QuanLyKhoWeb.Services.Ai
{
    // Cấu hình trợ lý AI, đọc từ section "Ai" trong appsettings/user-secrets.
    // ApiKey KHÔNG được ghi vào appsettings.json; đặt bằng:
    //   dotnet user-secrets set "Ai:ApiKey" "<key>"
    public class AiCauHinh
    {
        public const string SectionName = "Ai";

        // Khóa Google AI Studio. Rỗng => trợ lý báo "chưa cấu hình" thay vì gọi mạng.
        public string ApiKey { get; set; } = string.Empty;

        // Tên model Gemini (vd: gemini-3.5-flash-lite).
        public string Model { get; set; } = "gemini-3.5-flash-lite";

        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

        public int MaxTokens { get; set; } = 1024;

        // Số vòng gọi model tối đa trong một lượt (mỗi vòng có thể kèm 1 lượt gọi tool).
        public int SoVongToiDa { get; set; } = 5;

        public int TimeoutGiay { get; set; } = 60;

        // Thời gian sống của một "hành động chờ xác nhận" trong bộ nhớ đệm.
        public int PhutChoXacNhan { get; set; } = 10;

        public bool DaCauHinh => !string.IsNullOrWhiteSpace(ApiKey);
    }
}
