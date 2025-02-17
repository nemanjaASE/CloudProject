using Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        [Authorize(Roles = Roles.Administrator)]
        public IActionResult Index()
        {
            return View();
        }

		[Authorize(Roles = Roles.Administrator)]
		public IActionResult AdminDashboard()
		{
			return View();
		}

		[Authorize(Roles = Roles.Student)]
		public IActionResult StudentDashboard()
		{
			return View();
		}

		[Authorize(Roles = Roles.Professor)]
		public IActionResult ProfessorDashboard()
		{
			return View();
		}
	}
}
