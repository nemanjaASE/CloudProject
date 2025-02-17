using Common.Interfaces;
using Common.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Models;
using Common.Models;
using Common.Enums;
using Common.Helpers;
using Common.Constants;
using Common.Guard;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace WebApp.Controllers
{
	[Authorize(Roles = Roles.Student)]
	public class StudentController : Controller
	{
		ServiceClientFactory _proxy;
		private static readonly List<string> AllowedExtensions = [".pdf", ".txt", ".py"];
		public StudentController()
		{
			_proxy = new();
		}
		private async Task<IStudent> CreateStudentProxy()
		{
			var studentService = await _proxy.CreateServiceProxyAsync<IStudent>(ApiRoutes.StudentService, false);

			Guard.EnsureNotNull(studentService, nameof(studentService));

			return studentService;
		}

		public IActionResult Index()
		{
			return View();
		}

		[HttpGet]
		public async Task<IActionResult> ManageDocuments(int page = 1, int pageSize = 5)
		{
			var userId = GetLoggeUserId().Result;

			var _studentService = await CreateStudentProxy();

			try
			{
				var courses = await _studentService.GetAllCourses();

				if (courses is null || courses.Count.Equals(0))
				{
					TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
					Redirect("ManageDocuments");
				}

				var coursesList = new List<SelectListItem>();

				courses.ForEach(c => coursesList.Add(new SelectListItem()
				{
					Value = c.CourseId.ToString(),
					Text = $"{c.Title} ({c.AuthorName})",
				}));

				var (documents, numberOfDocuments) = await _studentService.GetDocumentsForStudent(userId, page, pageSize);
				if (documents is null)
				{
					return View(new ManageDocumentsViewModel()
					{
						Courses = coursesList,
						Documents = [],
						Page = page,
						PageSize = pageSize,
						TotalCount = 0,
					});
				}

				List<DocumentViewModel> model = [];

				documents.ForEach(doc => model.Add(
					new DocumentViewModel() { FileName = doc.FileName, Extension = doc.Extension, Version = doc.Version, CourseName = doc.CourseName }));

				return View(new ManageDocumentsViewModel()
				{
					Courses = coursesList,
					Documents = model,
					Page = page,
					PageSize = pageSize,
					TotalCount = numberOfDocuments,
				});
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return Redirect("ManageDocuments");
			}
		}

		[HttpGet]
		public async Task<IActionResult> Review(string fileName, int version, string extension)
		{
			var userId = GetLoggeUserId().Result;

			try
			{
				var _studentService = await CreateStudentProxy();

				var analysis = await _studentService.GetAnalysis(userId, $"{fileName}_{version}.{extension}");

				if (analysis is null)
				{
					TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
					return RedirectToAction("ManageDocuments");
				}

				var progress = await _studentService.GetProgress(userId, fileName);

				var documentModel = new DocumentViewModel { FileName = fileName, Extension = extension, Version = version, Analysis = analysis, ProgressView = progress };

				return View(documentModel);
			}
			catch (Exception) 
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ManageDocuments");
			}
		}

		[HttpGet]
		public async Task<IActionResult> Delete(string fileName)
		{
			var userId = GetLoggeUserId().Result;

			var _studentService = await CreateStudentProxy();

			try
			{
				var isSuccess = await _studentService.DeleteDocument(userId, fileName);

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
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ManageDocuments");
			}
		}

		[HttpGet]
		public async Task<IActionResult> ProgressOverTime()
		{
			try
			{
				var studentId = GetLoggeUserId().Result;

				var _studentService = await CreateStudentProxy();

				var result = await _studentService.GetProgressAllDocuments(studentId);


				var documentModel = new DocumentViewModel { ProgressView = result };

				return View("Progress",documentModel);
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ManageDocuments");
			}
		}

		[HttpGet]
		public async Task<IActionResult> Download(string fileName, int version, string extension)
		{
			var userId = GetLoggeUserId().Result;

			var _studentService = await CreateStudentProxy();

			try
			{
				var (content, contentType) = await _studentService.DownloadDocument(
				new DocumentInfo() { Extension = extension, FileName = fileName, Version = version },
				userId);

				return File(content, contentType);
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ManageDocuments");
			}
		}

		[HttpPost]
		public async Task<IActionResult> RollbackVersion(string fileName, int version)
		{
			var userId = GetLoggeUserId().Result;

			var _studentService = await CreateStudentProxy();

			try
			{
				var success = await _studentService.RollbackToPreviousVersionAsync(userId, fileName, version);

				if (success)
				{
					TempData[Messages.SuccessMessage] = "Document successfully rolled back to the previous version.";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to rollback to the previous version.";
				}

				return RedirectToAction("ManageDocuments");
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ManageDocuments");
			}
		}

		[HttpPost]
		public async Task<IActionResult> AnalyzeManually(string fileName, string extension)
		{
			var userId = GetLoggeUserId().Result;

			var _studentService = await CreateStudentProxy();

			try
			{
				var (isSuccessfully, estimation) = await _studentService.ProcessDocumentManually(new DocumentDTO()
				{
					FileName = fileName,
					Extension = GetFileTypeFromString(extension),
					UserId = userId,
				});

				if (isSuccessfully)
				{
					TempData[Messages.SuccessMessage] = $"Document will be analyzed in {estimation}s."; ;
					return RedirectToAction("ManageDocuments");
				}
				else if (!isSuccessfully && estimation.Equals(-1))
				{
					TempData[Messages.ErrorMessage] = "Failed to start analyzing document. You reached your limit. Try again later!";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to analyze document!";
				}

				return RedirectToAction("ManageDocuments");
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ManageDocuments");
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> NewVersion(IFormFile file, string fileName, string extension)
		{
			try
			{
				if (!IsValidFile(file, out string errorMessage))
				{
					TempData[Messages.ErrorMessage] = errorMessage;
					return RedirectToAction("ManageDocuments");
				}

				if (!CompareFiles(Path.GetExtension(file.FileName).Split('.')[1].ToLower(), Path.GetFileNameWithoutExtension(file.FileName), extension, fileName, out errorMessage))
				{
					TempData[Messages.ErrorMessage] = errorMessage;
					return RedirectToAction("ManageDocuments");
				}

				var _studentService = await CreateStudentProxy();

				bool isSuccessfully = false;
				var userId = GetLoggeUserId().Result;
				var contentType = file.ContentType;
				double estimation = 0;

				using (var stream = file.OpenReadStream())
				{
					var fileBytes = await ConvertStreamToByteArrayAsync(stream);
					(isSuccessfully, estimation) = await _studentService.ProcessNewVersionAsync(new DocumentDTO()
					{
						FileName = fileName,
						Extension = GetFileTypeFromString(extension),
						ContentType = contentType,
						Content = fileBytes,
						UserId = userId,
					});
				}

				if (isSuccessfully)
				{
					TempData[Messages.SuccessMessage] = $"New version of document uploaded successfully! Document will be analyzed in {estimation}s.";
					return RedirectToAction("ManageDocuments");
				}
				else if (!isSuccessfully && estimation.Equals(-1))
				{
					TempData[Messages.ErrorMessage] = "Failed to upload new version of document. You reached your limit. Try again later!";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to upload new version of document!";
				}

				return RedirectToAction("ManageDocuments");
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ManageDocuments");
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Upload(IFormFile file, string selectedCourse)
		{
			try
			{
				if (!IsValidFile(file, out string errorMessage))
				{
					TempData[Messages.ErrorMessage] = errorMessage;
					return RedirectToAction("ManageDocuments");
				}

				if (selectedCourse is null)
				{
					TempData[Messages.ErrorMessage] = "Select a course name!";
					return RedirectToAction("ManageDocuments");
				}

				var _studentService = await CreateStudentProxy();

				var fileName = Path.GetFileNameWithoutExtension(file.FileName);

				var userId = GetLoggeUserId().Result;
				var extension = GetFileTypeFromString(Path.GetExtension(file.FileName).ToLower());
				var contentType = file.ContentType;

				bool isSuccessfully = false;
				double estimation = 0;

				using (var stream = file.OpenReadStream())
				{
					var fileBytes = await ConvertStreamToByteArrayAsync(stream);
					(isSuccessfully, estimation) = await _studentService.ProcessDocumentAsync(new DocumentDTO()
					{
						FileName = fileName,
						Extension = extension,
						ContentType = contentType,
						Content = fileBytes,
						UserId = userId,
						CourseId = Guid.Parse(selectedCourse),
					});
				}

				if (isSuccessfully)
				{
					TempData[Messages.SuccessMessage] = $"Document uploaded successfully! Document will be analyzed in {estimation}s.";
					return RedirectToAction("ManageDocuments");
				}
				else if (!isSuccessfully && estimation.Equals(-1))
				{
					TempData[Messages.ErrorMessage] = "Failed to start analyzing document. You reached your limit. Try again later!";
				}
				else
				{
					TempData[Messages.ErrorMessage] = "Failed to upload document!";
				}

				return RedirectToAction("ManageDocuments");
			}
			catch (Exception)
			{
				TempData[Messages.ErrorMessage] = "Some error occured. Please contact administrator.";
				return RedirectToAction("ManageDocuments");
			}
		}

		private static async Task<byte[]> ConvertStreamToByteArrayAsync(Stream stream)
		{
			using var memoryStream = new MemoryStream();
			await stream.CopyToAsync(memoryStream);
			return memoryStream.ToArray();
		}
		private async Task<Guid> GetLoggeUserId()
		{
			var idClaim = User.Claims.FirstOrDefault(c => c.Type.Equals(System.Security.Claims.ClaimTypes.NameIdentifier));
			var userId = Guid.Parse(idClaim?.Value);

			return userId;
		}
		private static bool IsValidFile(IFormFile file, out string errorMessage)
		{
			errorMessage = string.Empty;

			if (file == null || file.Length == 0)
			{
				errorMessage = "Please select a valid file.";
				return false;
			}

			var extension = Path.GetExtension(file.FileName).ToLower();

			if (!AllowedExtensions.Contains(extension))
			{
				errorMessage = "Invalid file type. Only .pdf and .txt files are allowed.";
				return false;
			}

			return true;
		}
		private static bool CompareFiles(string extension1, string fileName1, string extension2, string fileName2, out string errorMessage)
		{
			errorMessage = string.Empty;

			if (!extension1.Equals(extension2))
			{
				errorMessage = "File extension must be the same as the previous one.";
				return false;
			}

			if (!fileName1.Equals(fileName2))
			{
				errorMessage = "File name must be the same as the previous one.";
				return false;
			}

			return true;
		}
		private static DocumentExtension GetFileTypeFromString(string fileExtension)
		{
			if (fileExtension.StartsWith("."))
			{
				fileExtension = fileExtension.Substring(1);
			}

			Enum.TryParse(fileExtension, ignoreCase: true, out DocumentExtension fileType);
			return fileType;
		}
	}
}
