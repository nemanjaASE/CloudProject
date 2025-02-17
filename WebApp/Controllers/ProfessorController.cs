using Common.Constants;
using Common.Guard;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using WebApp.Models;

namespace WebApp.Controllers
{
	[Authorize(Roles = Roles.Professor)]
	public class ProfessorController : Controller
	{
		ServiceClientFactory _proxy;
		public ProfessorController()
		{
			_proxy = new();
		}

		private async Task<IProfessor> CreateProfessorProxy()
		{
			var professorService = await _proxy.CreateServiceProxyAsync<IProfessor>(ApiRoutes.ProfessorService, false);

			Guard.EnsureNotNull(professorService, nameof(professorService));

			return professorService;
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> GenerateReport(CourseFilterViewModel filter)
		{
			try
			{
				var professorId = GetLoggeUserId().Result;

				var _professorService = await CreateProfessorProxy();

				var result = await _professorService.GenerateReportPdf(professorId, filter.StartDate, filter.EndDate, filter.SelectedCourse);

				return File(result, Types.PdfContentType);
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ProfessorDashboard", "Home");
			}
		}

		[HttpGet]
		public async Task<IActionResult> ViewProgress(CourseFilterViewModel filter)
		{
			try
			{
				var professorId = GetLoggeUserId().Result;

				var _professorService = await CreateProfessorProxy();

				var courses = await _professorService.GetCoursesForProfessor(professorId);

				filter.Courses = courses.Select(c => new SelectListItem
				{
					Value = c.CourseId.ToString(),
					Text = c.Title
				}).ToList();

				return View(filter);
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ProfessorDashboard", "Home");
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddReference(Guid studentId, string reference, string author, string fileName, string extension, string version)
		{
			try
			{
				var _professorService = await CreateProfessorProxy();

				var isSucces = await _professorService.UpdateAnalysisReference(reference, author, studentId, $"{fileName}_{version}.{extension}");

				if (isSucces)
				{
					TempData[Messages.SuccessMessage] = "New reference added sucessfully!";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to add reference.";
				}

				return RedirectToAction("ManageAnalyses");
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ProfessorDashboard", "Home");
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddImprovements(Guid studentId, string location, string suggestion, string fileName, string extension, string version)
		{
			try
			{
				var _professorService = await CreateProfessorProxy();

				var isSucces = await _professorService.UpdateAnalysisSuggestion(suggestion, location, studentId, $"{fileName}_{version}.{extension}");

				if (isSucces)
				{
					TempData[Messages.SuccessMessage] = "New improvement added sucessfully!";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to add improvement.";
				}

				return RedirectToAction("ManageAnalyses");
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ProfessorDashboard", "Home");
			}
		}

		[HttpGet]
		public async Task<IActionResult> Download(string fileName, int version, string extension, Guid userId)
		{
			try
			{
				var _professorService = await CreateProfessorProxy();

				var (content, contentType) = await _professorService.DownloadDocument(
					new DocumentInfo() { Extension = extension, FileName = fileName, Version = version },
					userId);

				return File(content, contentType);
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ProfessorDashboard", "Home");
			}
		}

		[HttpGet]
		public async Task<IActionResult> Review(string fileName, int version, string extension, Guid userId)
		{
			try
			{
				var _professorService = await CreateProfessorProxy();

				var analysis = await _professorService.GetAnalysis(userId, $"{fileName}_{version}.{extension}");

				if (analysis is null)
				{
					TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
					return RedirectToAction("ManageAnalyses");
				}

				var progress = await _professorService.GetProgress(userId, fileName);

				var documentModel = new DocumentViewModel { FileName = fileName, Extension = extension, Version = version, Analysis = analysis, ProgressView = progress, StudentId = userId };

				return View(documentModel);
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ProfessorDashboard", "Home");
			}
		}

		[HttpGet]
		public async Task<IActionResult> ManageAnalyses(int page = 1, int pageSize = 5)
		{
			try
			{
				var _professorService = await CreateProfessorProxy();

				var (users, totalUsers) = await _professorService.GetStudentsPaged(page, pageSize);

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
				return RedirectToAction("ProfessorDashboard", "Home");
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddNewCourse(string title, string description)
		{
			try
			{
				var _professorService = await CreateProfessorProxy();

				var authorId = GetLoggeUserId().Result;

				var isSucces = await _professorService.AddNewCourse(title, description, authorId);

				if (isSucces)
				{
					TempData[Messages.SuccessMessage] = "New course added sucessfully!";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to add new course.";
				}

				return RedirectToAction("ManageCourses");
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ProfessorDashboard", "Home");
			}
		}
		[HttpGet]
		public async Task<IActionResult> Delete(Guid courseId)
		{
			var _professorService = await CreateProfessorProxy();

			try
			{
				var professorId = GetLoggeUserId().Result;

				var isSuccess = await _professorService.DeleteCourse(courseId, professorId);

				if (isSuccess)
				{
					TempData[Messages.SuccessMessage] = "Course deleted successfully!";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to delete course!";
				}

				return RedirectToAction("ManageCourses");
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured.";
				return RedirectToAction("ManageCourses");
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateCourse(Guid courseId, string title, string description)
		{
			try
			{
				var _professorService = await CreateProfessorProxy();

				var authorId = GetLoggeUserId().Result;

				var isSucces = await _professorService.UpdateCourse(new Course()
				{
					Title = title,
					Description = description,
					AuthorId = authorId,
					CourseId = courseId,
				});

				if (isSucces)
				{
					TempData[Messages.SuccessMessage] = "Course updated sucessfully!";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to update course.";
				}

				return RedirectToAction("ManageCourses");
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ProfessorDashboard", "Home");
			}
		}
		[HttpGet]
		public async Task<IActionResult> ManageCourses(int page = 1, int pageSize = 5)
		{
			try
			{
				var _professorService = await CreateProfessorProxy();

				var authorId = GetLoggeUserId().Result;

				var (courses, totalCourses) = await _professorService.GetCoursesPaged(page, pageSize, authorId);

				var model = new PagedCourseViewModel()
				{
					Courses = courses.Select(c => new CourseViewModel
					{
						Title = c.Title,
						Description = c.Description,
						CreatedDate = c.CreatedDate,
						AuthorId = c.AuthorId,
						CourseId = c.CourseId,
					}),
					Page = page,
					PageSize = pageSize,
					TotalCount = totalCourses,
				};

				return View(model);
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ProfessorDashboard", "Home");
			}
		}

		[HttpGet]
		public async Task<IActionResult> ShowUserAnalyses(Guid id)
		{
			try
			{
				var _professorService = await CreateProfessorProxy();

				var professorId = GetLoggeUserId().Result;

				var result = await _professorService.GetDocumentsForStudent(professorId, id);

				if (result is null)
				{
					return View(null);
				}

				List<DocumentViewModel> documents = [];

				result.ForEach(doc => documents.Add(
					new DocumentViewModel() { FileName = doc.FileName, Extension = doc.Extension, Version = doc.Version, CourseName = doc.CourseName, StudentId = id }));

				return View(documents);
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ProfessorDashboard", "Home");
			}
		}

		private async Task<Guid> GetLoggeUserId()
		{
			var idClaim = User.Claims.FirstOrDefault(c => c.Type.Equals(System.Security.Claims.ClaimTypes.NameIdentifier));
			var userId = Guid.Parse(idClaim?.Value);

			return userId;
		}
	}
}
