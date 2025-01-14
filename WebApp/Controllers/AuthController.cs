using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Common.Interfaces;
using Common.DTO;
using Common.Enums;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization;

namespace WebApp.Controllers
{
	public class AuthController : Controller
	{
		private readonly IAuth _authService;

		public AuthController()
		{
			var serviceProxyFactory = new ServiceProxyFactory((callbackClient) =>
			{
				return new FabricTransportServiceRemotingClientFactory(
					new FabricTransportRemotingSettings
					{
						ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Default
					},
					callbackClient);
			});

			var serviceUri = new Uri("fabric:/EduAnalyzer/AuthService");
			_authService = serviceProxyFactory.CreateServiceProxy<IAuth>(serviceUri);
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

					return RedirectToAction("ProfesorDashboard", "Home");
				} else
				{
					return RedirectToAction("StudentDashboard", "Home");
				}
			}

			ViewData["ErrorMessage"] = "Wrong email or password.";
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
