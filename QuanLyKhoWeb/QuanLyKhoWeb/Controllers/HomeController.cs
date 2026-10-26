using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyKhoWeb.Models;
using System.Diagnostics;

namespace QuanLyKhoWeb.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // AllowAnonymous: toàn bộ app yêu cầu đăng nhập mặc định (AuthorizeFilter global trong
        // Program.cs), nhưng trang lỗi (app.UseExceptionHandler("/Home/Error")) phải luôn xem
        // được kể cả khi lỗi xảy ra trước khi đăng nhập, nếu không sẽ bị redirect vòng về trang đăng nhập.
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}