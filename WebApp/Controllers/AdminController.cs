using Common.Interfaces;
using Common.Models;
using Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Newtonsoft.Json;
using WebApp.Models;

namespace WebApp.Controllers
{
	public class AdminController : Controller
	{
		private readonly IAdmin _adminService;
		public AdminController()
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

			var serviceUri = new Uri("fabric:/EduAnalyzer/AdministratorService");
			_adminService = serviceProxyFactory.CreateServiceProxy<IAdmin>(serviceUri);
		}

		[Authorize(Roles = "Administrator")]
		public async Task<IActionResult> ManageUsers(int page = 1, int pageSize = 5)
		{
			var (users, totalUsers) = await _adminService.GetUsersPaged(page, pageSize);

			var model = new PagedUserViewModel
			{
				Users = users.Select(u => new UserViewModel
				{
					Id = u.Id,
					FirstName = u.FirstName,
					LastName = u.LastName,
					Email = u.Email,
					Role = u.Role
				}),
				Page = page,
				PageSize = pageSize,
				TotalCount = totalUsers,
			};

			return View(model);
		}

		[Authorize(Roles = "Administrator")]
		[HttpGet]
		public async Task<IActionResult> EditUser(Guid id)
		{
			User user = await _adminService.GetUserById(id);

			var userModel = new UserViewModel
			{
				Id = user.Id,
				FirstName = user.FirstName,
				LastName = user.LastName,
				Email = user.Email,
				Role = user.Role
			};

			return View(userModel);
		}

		[Authorize(Roles = "Administrator")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditUser(UserViewModel model)
		{
			if (ModelState.IsValid)
			{
				bool result = await _adminService.UpdateUser(new User
				{
					Id = model.Id,
					FirstName = model.FirstName,
					LastName = model.LastName,
					Email = model.Email,
					Role = model.Role
				});

				if(result)
				{
					TempData["SuccessMessage"] = "User updated successfully!";
				}
				else
				{
					TempData["ErrorMessage"] = "Failed to update user!";
				}

				return RedirectToAction("ManageUsers");
			}

			return View(model);
		}

		[Authorize(Roles = "Administrator")]
		[HttpGet]
		public IActionResult CreateUser()
		{
			return View();
		}

		[Authorize(Roles = "Administrator")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateUser(NewUserViewModel model)
		{
			if (ModelState.IsValid)
			{
				bool result = await _adminService.AddNewUser(new User
				{
					FirstName = model.FirstName,
					LastName = model.LastName,
					Email = model.Email,
					Role = EnumParser.GetUserRoleTypeFromString(model.Role),
					Password = model.Password,
				});

				if (result)
				{
					TempData["SuccessMessage"] = "User added successfully!";
				}
				else
				{
					TempData["ErrorMessage"] = "Failed to add user!";
				}

				return RedirectToAction("ManageUsers");
			}

			return View(model);
		}

		[Authorize(Roles = "Administrator")]
		[HttpGet]
		public async Task<IActionResult> DeleteUser(Guid id)
		{
			bool result = await _adminService.DeleteUser(id);

			if (result)
			{
				TempData["SuccessMessage"] = "User deleted successfully!";
			}
			else
			{
				TempData["ErrorMessage"] = "Failed to delete user!";
			}

			return RedirectToAction("ManageUsers");
		}
	}
}
