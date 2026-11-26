namespace QuanLyKhoWeb.Middlewares
{
    // Gắn header bảo mật cho mọi response: chống MIME sniffing, chống nhúng frame,
    // giới hạn nguồn tài nguyên (CSP) và tắt các quyền thiết bị không dùng tới.
    // Đặt sớm trong pipeline để cả file tĩnh và trang lỗi cũng có header.
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            // camera=(self): chỉ trang của chính ứng dụng được xin quyền camera (dùng cho quét mã SKU);
            // các quyền còn lại vẫn tắt vì app không dùng tới.
            headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=(), payment=()";

            // View vẫn còn inline <script>, handler on* và style inline nên phải cho phép
            // 'unsafe-inline'; siết lại khi đã chuyển hết sang file JS/CSS riêng.
            headers["Content-Security-Policy"] = string.Join("; ", new[]
            {
                "default-src 'self'",
                "script-src 'self' 'unsafe-inline'",
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
                "font-src 'self' https://fonts.gstatic.com",
                "img-src 'self' data: blob:",
                "media-src 'self' blob: mediastream:",
                "connect-src 'self'",
                "frame-ancestors 'none'",
                "base-uri 'self'",
                "form-action 'self'"
            });

            await _next(context);
        }
    }
}