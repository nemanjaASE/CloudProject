using Common.Interfaces;
using Common.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using WebApp.Models;
using Common.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.VisualBasic.FileIO;
using Common.Enums;
using System.Runtime.CompilerServices;
using System.Reflection.Metadata;

namespace WebApp.Controllers
{
	public class StudentController : Controller
	{
		private readonly IStudent _studentService;
		private static readonly List<string> AllowedExtensions = [".pdf", ".txt"];
		public StudentController()
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

			var serviceUri = new Uri("fabric:/EduAnalyzer/StudentService");
			_studentService = serviceProxyFactory.CreateServiceProxy<IStudent>(serviceUri);
		}
		public IActionResult Index()
		{
			return View();
		}

		[Authorize(Roles = "Student")]
		public async Task<IActionResult> ManageDocuments()
		{
			var userId = GetLoggeUserId().Result;

			var result = await _studentService.GetDocumentsForStudent(userId);

			if (result is null)
			{
				return View(null);
			}

			List<DocumentViewModel> documents = [];

			result.ForEach(doc => documents.Add(
				new DocumentViewModel() { FileName = doc.FileName, Extension = doc.Extension, Version = doc.Version }));

			return View(documents);
		}

		[Authorize(Roles = "Student")]
		[HttpGet]
		public async Task<IActionResult> Review(string fileName, int version, string extension)
		{
			var documentModel = new DocumentViewModel { FileName = fileName, Extension = extension, Version = version };

			return View(documentModel);
		}

		[Authorize(Roles = "Student")]
		[HttpGet]
		public async Task<IActionResult> Delete(string fileName)
		{
			var userId = GetLoggeUserId().Result;

			var isSuccess = await _studentService.DeleteDocument(userId, fileName);

			if (isSuccess)
			{
				TempData["SuccessMessage"] = "Document deleted successfully!";
				return RedirectToAction("ManageDocuments");
			}

			TempData["ErrorMessage"] = "Failed to delete document!";

			return RedirectToAction("ManageDocuments");
		}

		[Authorize(Roles = "Student")]
		[HttpGet]
		public async Task<IActionResult> Download(string fileName, int version, string extension)
		{
			var userId = GetLoggeUserId().Result;

			var (content, contentType) = await _studentService.DownloadDocument(
				new DocumentInfo() { Extension = extension, FileName = fileName, Version = version}, 
				userId);

			return File(content, contentType);
		}

		[HttpPost]
		[Authorize(Roles = "Student")]
		public async Task<IActionResult> RollbackVersion(string fileName)
		{
			var userId = GetLoggeUserId().Result;

			var success = await _studentService.RollbackToPreviousVersionAsync(userId, fileName);

			if (success)
			{
				TempData["Message"] = "Document successfully rolled back to the previous version.";
				return RedirectToAction("Review", new { fileName });
			}

			TempData["Error"] = "Failed to rollback to the previous version.";

			return RedirectToAction("ManageDocuments");
		}

		[Authorize(Roles = "Student")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> NewVersion(IFormFile file, string fileName, string extension)
		{
			if (!IsValidFile(file, out string errorMessage))
			{
				TempData["ErrorMessage"] = errorMessage;
				return RedirectToAction("ManageDocuments");
			}

			if (!CompareFiles(Path.GetExtension(file.FileName).Split('.')[1].ToLower(), Path.GetFileNameWithoutExtension(file.FileName), extension, fileName, out errorMessage))
			{
				TempData["ErrorMessage"] = errorMessage;
				return RedirectToAction("ManageDocuments");
			}

			bool isSuccess = false;
			var userId = GetLoggeUserId().Result;
			var contentType = file.ContentType;

			using (var stream = file.OpenReadStream())
			{
				var fileBytes = await ConvertStreamToByteArrayAsync(stream);
				isSuccess = await _studentService.ProcessNewVersionAsync(new DocumentDTO()
				{
					FileName = fileName,
					Extension = GetFileTypeFromString(extension),
					ContentType = contentType,
					Content = fileBytes,
					UserId = userId,
				});
			}

			if (isSuccess)
			{
				TempData["SuccessMessage"] = "New version of document uploaded successfully!";
				return RedirectToAction("ManageDocuments", "Student");
			}

			TempData["ErrorMessage"] = "Failed to upload new version of document!";

			return RedirectToAction("ManageDocuments", "Student");
		}

		[Authorize(Roles = "Student")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Upload(IFormFile file)
		{
			if (!IsValidFile(file, out string errorMessage))
			{
				TempData["ErrorMessage"] = errorMessage;
				return RedirectToAction("ManageDocuments");
			}

			var fileName = Path.GetFileNameWithoutExtension(file.FileName);

			var userId = GetLoggeUserId().Result;
			var extension = GetFileTypeFromString(Path.GetExtension(file.FileName).ToLower());
			var contentType = file.ContentType;

			bool isSuccess = false;

			using (var stream = file.OpenReadStream())
			{	
				var fileBytes = await ConvertStreamToByteArrayAsync(stream);
				isSuccess = await _studentService.ProcessDocumentAsync(new DocumentDTO()
				{
					FileName = fileName,
					Extension = extension,
					ContentType = contentType,
					Content = fileBytes,
					UserId = userId,
				});
			}

			if (isSuccess)
			{
				TempData["SuccessMessage"] = "Document uploaded successfully!";
				return RedirectToAction("ManageDocuments");
			}
	
			TempData["ErrorMessage"] = "Failed to upload document!";

			return RedirectToAction("ManageDocuments", "Student");
		}

		private static async Task<byte[]> ConvertStreamToByteArrayAsync(Stream stream)
		{
			using (var memoryStream = new MemoryStream())
			{
				await stream.CopyToAsync(memoryStream);
				return memoryStream.ToArray();
			}
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
			Enum.TryParse(fileExtension, ignoreCase: true, out DocumentExtension fileType);
			return fileType;
		}
	}
}
