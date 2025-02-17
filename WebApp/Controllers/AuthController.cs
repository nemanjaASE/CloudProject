using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Common.Interfaces;
using Common.DTO;
using Common.Enums;
using Microsoft.AspNetCore.Authorization;
using Common.Helpers;
using Common.Constants;
using Common.Guard;

namespace WebApp.Controllers
{
	public class AuthController : Controller
	{
		ServiceClientFactory? _proxy;
		public AuthController()
		{
			_proxy = new();
		}

		private async Task<IAuth> CreateAuthProxy()
		{
			var authService = await _proxy.CreateServiceProxyAsync<IAuth>(ApiRoutes.AuthService, false);

			Guard.EnsureNotNull(authService, nameof(authService));

			return authService;
		}

		[AllowAnonymous]
		public IActionResult Login()
		{
			return View(new LoginViewModel());
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Login(LoginViewModel model)
		{
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var _authService = await CreateAuthProxy();

			LoggedUserDTO loggedUser = await _authService.Login(new UserLoginDTO()
			{
				Email = model.Email,
				Password = model.Password,
			});

			if (loggedUser is not null)
			{
				var claims = new List<Claim>
				{
					new Claim(ClaimTypes.Name, model.Email),
					new Claim(ClaimTypes.Email, model.Email),
					new Claim(ClaimTypes.Role, loggedUser.Role.ToString()),
					new Claim(ClaimTypes.NameIdentifier, loggedUser.UserId.ToString()),
				};

				var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
				var principal = new ClaimsPrincipal(identity);

				await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
					new AuthenticationProperties { IsPersistent = model.RememberMe });

				if (loggedUser.Role.Equals(UserRole.Administrator))
				{
					return RedirectToAction("AdminDashboard", "Home");
				}
				else if (loggedUser.Role.Equals(UserRole.Professor)){

					return RedirectToAction("ProfessorDashboard", "Home");
				} else
				{
					return RedirectToAction("StudentDashboard", "Home");
				}
			}

			ViewData[Messages.ErrorMessage] = "Wrong email or password.";
			return View(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Logout()
		{
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			return RedirectToAction("Login", "Auth");
		}

		[HttpGet]
		public IActionResult AccessDenied()
		{
			return View();
		}
	}
}
