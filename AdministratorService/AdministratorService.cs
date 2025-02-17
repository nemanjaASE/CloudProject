using System.Fabric;
using Common.Constants;
using Common.DTO;
using Common.Guard;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace AdministratorService
{

	internal sealed class AdministratorService(StatelessServiceContext context) : StatelessService(context), IAdmin
    {
        private IUser? _userService;
		private IDocument? _documentService;
		private IDocumentProcessing? _documentProcessingService;
		private IAnalysis? _analysisService;
		private IAuth? _authService;
		private readonly ServiceClientFactory _factory = new();

		#region SETTINGS
		public async Task<bool> UpdateSettings(Settings settings)
	    {
			try
			{
				if (await _documentProcessingService.UpdateSettings(settings.ModelSettings))
				{
					return await _documentService.SetRateLimitSettings(settings.RateLimit.MaxAttempts, settings.RateLimit.TimeInterval);
				}

				return false;
			}
			catch (Exception e)
			{

				throw new Exception(e.Message);
			}
	    }
		public async Task<Settings?> GetSettings()
		{
			try
			{
				var rateLimit = await _documentService.GetRateLimitSettings();

				if (rateLimit is null)
				{
					return null;
				}

				var modelSettings = await _documentProcessingService.GetSettings();

				if (modelSettings is null)
				{
					return null;
				}

				return new Settings() { RateLimit = rateLimit, ModelSettings = modelSettings };
			}
			catch (Exception e)
			{ 
				throw new Exception(e.Message);
			}
		}
		public async Task<ModelListResponse?> GetActiveModels()
		{
			try
			{
				var respond = await GroqApi.GetAllModels();

				return new ModelListResponse()
				{
					Data = respond
				};
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<bool> SetRateLimitSettings(uint maxAttempts, uint timeInterval)
		{
			try
			{
				return await _documentService.SetRateLimitSettings(maxAttempts, timeInterval);
			}
			catch (Exception e)
			{

				throw new Exception(e.Message);
			}
		}
		#endregion
		#region USER
		public async Task<bool> AddNewUser(User user)
		{
			try
			{
				if (user is null)
				{
					return false;
				}

				if (await _userService.CheckIfUserExistsByEmail(user.Email))
				{
					return false;
				}

				return await _userService.AddNewUser(user);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<bool> DeleteUser(Guid id)
		{
			try
			{
				if (id.Equals(Guid.Empty))
				{
					return false;
				}

				if (await _userService.DeleteUser(id))
				{
					return await _documentService.DeleteAllDocumentsForUser(id);
				}

				return false;
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<List<User>> GetAllUsers()
		{
			try
			{
				return await _userService.GetAllUsers();
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<User?> GetUserById(Guid id)
		{
			try
			{
				if (id.Equals(Guid.Empty))
				{
					return null;
				}

				return await _userService.GetUserById(id);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<(List<User>, int)> GetUsersPaged(int page, int pageSize)
		{
			try
			{
				return await _userService.GetUsersPaged(page, pageSize);
			}
			catch (Exception e)
			{

				throw new Exception(e.Message);
			}
		}
		public async Task<bool> UpdateUser(User user)
		{
			try
			{
				if (user is null)
				{
					return false;
				}

				var existingUser = await _userService.GetUserById(user.Id);

				user.Password = existingUser.Password;

				if (existingUser is not null)
				{
					return await _userService.UpdateUser(user);
				}

				return false;
			}
			catch (Exception e)
			{

				throw new Exception(e.Message);
			}
		}
		public async Task<bool> ChangePassword(Guid userId, string newPassword)
		{
			try
			{
				return await _authService.ChangePassword(userId, newPassword);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		#endregion
		#region DOCUMENTS
		public async Task<(List<DocumentViewDTO>, int)> GetAllDocumentsPaged(int pageNumber, int pageSize)
		{
			try
			{
				var (documents, numOfDocuments) = await _documentService.GetAllDocumentsPaged(pageNumber, pageSize);

				List<DocumentViewDTO> result = [];

				foreach (var document in documents) {
					var (firstName, lastName) = await _userService.GetUserName(document.UserId);

					result.Add(new DocumentViewDTO()
					{
						StudentId = document.UserId,
						StudentFirstName = firstName,
						StudentLastName = lastName,
						CourseName = document.CourseName,
						Extension = document.Extension,
						FileName = document.FileName,
						Version = document.Version,
					});
				}

				return (result, numOfDocuments);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<(byte[]? content, string contentType)> DownloadDocument(DocumentInfo document, Guid studentId)
		{
			try
			{
				return await _documentService.DownloadDocument(document, studentId);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<bool> DeleteDocument(Guid studentId, string fileName)
		{
			try
			{
				if (await _documentService.DeleteDocument(studentId, fileName))
				{
					return await _analysisService.DeleteAnalysis(studentId, fileName);
				}

				return false;
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		#endregion
		#region ANALYSIS
		public async Task<AnalysisDTO?> GetAnalysis(Guid userId, string fileName)
		{
			try
			{
				return await _analysisService.GetAnalysis(userId, fileName);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}

		public async Task<ProgressDTO?> GetProgress(Guid userId, string fileName)
		{
			try
			{
				var progress = await _analysisService.GetProgress(userId, fileName);

				if (progress.Count.Equals(0))
				{
					return null;
				}

				var averageScore = Math.Round(progress.Average(p => p.Score));

				return new ProgressDTO()
				{
					Progress = progress,
					AverageScore = averageScore,
				};
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		#endregion
		protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
			return
			[
				new ServiceInstanceListener(serviceContext =>
					new FabricTransportServiceRemotingListener(
						serviceContext,
						this,
						new FabricTransportRemotingListenerSettings
							{
								ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.Default,
							}),
						"ServiceEndpointV2")
			];
		}
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
			_userService = await _factory.CreateServiceProxyAsync<IUser>(ApiRoutes.UserService, true);
			_documentService = await _factory.CreateServiceProxyAsync<IDocument>(ApiRoutes.DocumentService, true);
			_documentProcessingService = await _factory.CreateServiceProxyAsync<IDocumentProcessing>(ApiRoutes.DocumentProcessingService, true);
			_analysisService = await _factory.CreateServiceProxyAsync<IAnalysis>(ApiRoutes.AnalysisService, true);
			_authService = await _factory.CreateServiceProxyAsync<IAuth>(ApiRoutes.AuthService, false);

			Guard.EnsureNotNull(_userService, nameof(_userService));
			Guard.EnsureNotNull(_documentService, nameof(_documentService));
			Guard.EnsureNotNull(_analysisService, nameof(_analysisService));
			Guard.EnsureNotNull(_documentProcessingService, nameof(_documentProcessingService));
			Guard.EnsureNotNull(_authService, nameof(_authService));
		}
    }
}
