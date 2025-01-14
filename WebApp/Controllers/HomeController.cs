using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Models;

namespace WebApp.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        [Authorize(Roles = "Administrator")]
        public IActionResult Index()
        {
            return View();
        }

		[Authorize(Roles = "Administrator")]
		public IActionResult AdminDashboard()
		{
			return View();
		}

		[Authorize(Roles = "Student")]
		public IActionResult StudentDashboard()
		{
			return View();
		}
    }
}
