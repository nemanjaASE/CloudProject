using Common.Interfaces;
using Common.Models;
using Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Models;
using Common.Constants;
using Common.Guard;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace WebApp.Controllers
{
	[Authorize(Roles = Roles.Administrator)]
	public class AdminController : Controller
	{
		ServiceClientFactory? _proxy;
		public AdminController()
		{
			_proxy = new ServiceClientFactory();
		}

		private async Task<IAdmin> CreateAdminProxy()
		{
			var adminService = await _proxy.CreateServiceProxyAsync<IAdmin>(ApiRoutes.AdministratorService, false);
			Guard.EnsureNotNull(adminService, nameof(adminService));

			return adminService;
		} 

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateSettings(SettingsViewModel model, string[] SelectedRequirementsToRemove)
		{

			if (!ModelState.IsValid)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("Settings");
			}

			try
			{
				var _adminService = await CreateAdminProxy();

				var settings = await _adminService.GetSettings();

				var existingRequirements = settings.ModelSettings.AdditionalRequirements;

				existingRequirements = existingRequirements
					.Where(req => !SelectedRequirementsToRemove.Contains(req))
					.ToList();

				if (!string.IsNullOrWhiteSpace(model.NewRequirement))
				{
					existingRequirements.Add(model.NewRequirement);
				}

				model.Settings.ModelSettings.AdditionalRequirements = existingRequirements;

				var result = await _adminService.UpdateSettings(model.Settings);

				if (result)
				{
					TempData[Messages.SuccessMessage] = "Active model settings successfully updated.";
				}
				else
				{
						TempData[Messages.ErrorMessage] = "Failed to update active settings.";
				}
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
			}

			return RedirectToAction("Settings");
		}
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ChangePassword(Guid userId, string newPassword)
		{
			try
			{
				var _adminService = await CreateAdminProxy();

				var isSuccessful = await _adminService.ChangePassword(userId, newPassword);

				if (isSuccessful)
				{
					TempData[Messages.SuccessMessage] = "User password successfully changed.";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to change user password.";
				}
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured.";
			}

			return RedirectToAction("ManageUsers");

		}
		[HttpGet]
		public async Task<IActionResult> Settings()
		{
			try
			{
				var _adminService = await CreateAdminProxy();

				var settings = await _adminService.GetSettings();

				if (settings is null)
				{
					TempData[Messages.ErrorMessage] = "Cannot retrieve settings.";
					return RedirectToAction("Settings");
				}

				var models = await _adminService.GetActiveModels();

				if (models is null)
				{
					TempData[Messages.ErrorMessage] = "Cannot retrieve models.";
					return RedirectToAction("Settings");
				}

				var modelGroups = models.Data
										.GroupBy(m => m.Value.OwnedBy)
										.Select(group => new SettingsViewModel.OwnedModelsGroup
										{
											GroupName = group.Key,
											Models = group.Select(m => new SelectListItem
											{
												Value = m.Key.ToString(),
												Text = $"{m.Key} -  (CW {m.Value.ContextWindow} tokens)",
												Selected = m.Key.ToString() == settings.ModelSettings.ModelName,
											}).ToList(),

										}).ToList();

				var viewModel = new SettingsViewModel
				{
					Settings = settings,
					Models = modelGroups,
				};

				return View(viewModel);
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("AdminDashboard", "Home");
			}
		}
		[HttpGet]
		public async Task<IActionResult> ManageDocuments(int page = 1, int pageSize = 5)
		{
			try
			{
				var _adminService = await CreateAdminProxy();

				var (documents, totalDocuments) = await _adminService.GetAllDocumentsPaged(page, pageSize);

				List<DocumentViewModel> model = [];

				documents.ForEach(doc => model.Add(
					new DocumentViewModel(){ FileName = doc.FileName, Extension = doc.Extension, Version = doc.Version, 
											 CourseName = doc.CourseName, StudentFirstName = doc.StudentFirstName, StudentLastName = doc.StudentLastName, StudentId = doc.StudentId}));

				var ret = new ManageDocumentsViewModel()
				{
					Documents = model,
					Page = page,
					PageSize = pageSize,
					TotalCount = totalDocuments,
				};

				return View(ret);
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("AdminDashboard", "Home");
			}
		}
		[HttpGet]
		public async Task<IActionResult> Download(Guid studentId, string fileName, int version, string extension)
		{
			var _adminService = await CreateAdminProxy();

			try
			{
				var (content, contentType) = await _adminService.DownloadDocument(
				new DocumentInfo() { Extension = extension, FileName = fileName, Version = version },
				studentId);

				return File(content, contentType);
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured.";
				return RedirectToAction("ManageDocuments");
			}
		}
		[HttpGet]
		public async Task<IActionResult> Delete(Guid studentId, string fileName)
		{
			var _studentService = await CreateAdminProxy();

			try
			{
				var isSuccess = await _studentService.DeleteDocument(studentId, fileName);

				if (isSuccess)
				{
					TempData[Messages.SuccessMessage] = "Document deleted successfully!";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to delete document!";
				}

				return RedirectToAction("ManageDocuments");
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured.";
				return RedirectToAction("ManageDocuments");
			}
		}
		[HttpGet]
		public async Task<IActionResult> Review(Guid studentId, string fileName, int version, string extension)
		{
			try
			{
				var _adminService = await CreateAdminProxy();

				var analysis = await _adminService.GetAnalysis(studentId, $"{fileName}_{version}.{extension}");

				if (analysis is null)
				{
					TempData[Messages.ErrorMessage] = "Some error occured.";
					return RedirectToAction("ManageDocuments");
				}

				var progress = await _adminService.GetProgress(studentId, fileName);

				var documentModel = new DocumentViewModel { FileName = fileName, Extension = extension, Version = version, Analysis = analysis, ProgressView = progress };

				return View(documentModel);
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured.";
				return RedirectToAction("ManageDocuments");
			}
		}
		[HttpGet]
		public async Task<IActionResult> ManageUsers(int page = 1, int pageSize = 5)
		{
			try
			{
				var _adminService = await CreateAdminProxy();

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
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("AdminDashboard", "Home");
			}
		}
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateUser(UserViewModel model)
		{
			if (!ModelState.IsValid)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("AdminDashboard", "Home");
			}

			try
			{
				var _adminService = await CreateAdminProxy();

				bool result = await _adminService.UpdateUser(new User
				{
					Id = model.Id,
					FirstName = model.FirstName,
					LastName = model.LastName,
					Email = model.Email,
					Role = model.Role
				});

				if (result)
				{
					TempData[Messages.SuccessMessage] = "User updated successfully!";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to update user!";
				}

				return RedirectToAction("ManageUsers");
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("AdminDashboard", "Home");
			}
			
		}
		[HttpGet]
		public IActionResult CreateUser()
		{
			return View();
		}
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateUser(NewUserViewModel model)
		{
			var _adminService = await CreateAdminProxy();
			if(!ModelState.IsValid)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("AdminDashboard", "Home");
			}
			try
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
					TempData[Messages.SuccessMessage] = "User added successfully!";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to add user!";
				}

				return RedirectToAction("ManageUsers");
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("AdminDashboard", "Home");	
			}
		}
		[HttpGet]
		public async Task<IActionResult> DeleteUser(Guid id)
		{
			try
			{
				var _adminService = await CreateAdminProxy();

				bool result = await _adminService.DeleteUser(id);

				if (result)
				{
					TempData[Messages.SuccessMessage] = "User deleted successfully!";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to delete user!";
				}

				return RedirectToAction("ManageUsers");
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("AdminDashboard", "Home");
			}
		}
	}
}
