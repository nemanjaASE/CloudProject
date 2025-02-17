using System.Fabric;
using AutoMapper;
using Common.Constants;
using Common.DTO;
using Common.Guard;
using Common.Helpers;
using Common.Interfaces;
using Common.Mappers;
using Common.Models;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace StudentService
{
	internal sealed class StudentService(StatelessServiceContext context) : StatelessService(context), IStudent
    {
		private IDocument? _documentService;
		private IDocumentProcessing? _documentProcessingService;
		private IAnalysis? _analysisService;
		private IProcessingTimeEstimator? _processingTimeEstimator;
		private IUser? _userService;
		private ICourse? _courseService;
		private IMapper? _mapper;
		private readonly ServiceClientFactory _factory = new();

		public async Task<ProgressDTO> GetProgressAllDocuments(Guid userId)
		{
			try
			{
				var progress = await _analysisService.GetAllAnalysesForUser(userId);

				if (progress.Count.Equals(0)) { return null; }

				foreach (var item in progress)
				{
					var course = await _courseService.GetCourseById(item.CourseId);
					item.CourseName = course.Title;
					var (firstName, lastName) = await _userService.GetUserName(course.AuthorId);
					item.AuthorName = $"{firstName} {lastName}";
				}

				var averageScore = Math.Round(progress.Average(p => p.Score), 2);
				var numOfAnalyzed = progress.Count;
				var numOfNotAnalyzed = await _analysisService.GetNumOfDocuments(userId, Common.Enums.AnalysisStatus.NOT_ANALYZED);
				var numOfInProgress = await _analysisService.GetNumOfDocuments(userId, Common.Enums.AnalysisStatus.IN_PROGRESS);

				return new ProgressDTO()
				{
					Progress = progress,
					AverageScore = averageScore,
					NumOfAnalyzed = numOfAnalyzed,
					NumOfInProgress = numOfInProgress,
					NumOfNotAnalyzed = numOfNotAnalyzed,
					TotalDocuments = numOfAnalyzed + numOfNotAnalyzed + numOfInProgress,
				};
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<List<Course>> GetAllCourses()
		{
			try
			{
				var courses = await _courseService.GetAllCourses();

				foreach (var course in courses) 
				{
					var (firstName, lastName) = await _userService.GetUserName(course.AuthorId);

					course.AuthorName = firstName + " " + lastName;
				}

				return courses;
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<(bool, double)> ProcessDocumentManually(DocumentDTO documentDto)
		{
			var document = _mapper.Map<Document>(documentDto);

			try
			{
				var isLimited = await _documentService.CheckRateLimit(document.UserId);

				if (!isLimited) {
					return (false, -1);
				}

				var (version, _) = await _documentService.FindLatestVersion(document.UserId, document.FileName);
				var documentInfo = new DocumentInfo()
				{
					FileName = document.FileName,
					Extension = document.Extension.ToString().ToLower(),
					Version = version,
				};
				var (content, _) = await _documentService.DownloadDocument(documentInfo, document.UserId);

				int fileLength = FileHelper.GetLengthBasedOnExtension(content, documentDto.Extension);
				var estimation = Math.Round((await _processingTimeEstimator.EstimateTime(fileLength) / 1000.0), 2);

				if (!await _documentProcessingService.ProcessDocument(documentInfo, document.UserId))
				{
					return (false, 0);
				}

				var isSuccesfull = await _documentService.UpdateRateLimit(document.UserId);

				return (isSuccesfull, estimation);
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<(bool, double)> ProcessDocumentAsync(DocumentDTO documentDto)
		{
			var document = _mapper.Map<Document>(documentDto);
			try
			{ 
				if (await _documentService.CheckIfFileExist(document.UserId, document.FileName))
				{
					return (false, 0);
				}

				var isLimited = await _documentService.CheckRateLimit(document.UserId);

				if (!isLimited)
				{
					return (false, -1);
				}

				document.Version = 1;

				await _documentService.UploadDocumentAsync(document);

				int fileLength = FileHelper.GetLengthBasedOnExtension(document.Content, documentDto.Extension);
				var estimation =  Math.Round((await _processingTimeEstimator.EstimateTime(fileLength) / 1000.0), 2);

				if (!await _documentProcessingService.ProcessDocument(new DocumentInfo()
				{
					FileName = document.FileName,
					Extension = document.Extension.ToString().ToLower(),
					Version = 1,
					CourseId = document.CourseId,

				}, document.UserId))
				{
					return (false, 0);
				}

				var isSuccesfull = await _documentService.UpdateRateLimit(document.UserId);

				return (isSuccesfull, estimation);
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<bool> RollbackToPreviousVersionAsync(Guid userId, string fileName, int currentVersion)
		{
			try
			{
				if (currentVersion > 1)
				{
					if( await _documentService.DeleteSpecificDocumentVersion(userId, fileName, currentVersion))
					{
						return await _analysisService.DeleteAnalysis(userId, $"{fileName}_{currentVersion}"); 
					}
				}

				return false;
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<(bool, double)> ProcessNewVersionAsync(DocumentDTO documentDto)
		{
			var document = _mapper.Map<Document>(documentDto);

			try
			{
				var isLimited = await _documentService.CheckRateLimit(document.UserId);

				if (!isLimited)
				{
					return (false, -1);
				}

				var (version, courseId) = await _documentService.FindLatestVersion(document.UserId, document.FileName);

				version++;
				document.Version = version;
				document.CourseId = Guid.Parse(courseId);
				await _documentService.UploadNewVersionAsync(document);

				int fileLength = FileHelper.GetLengthBasedOnExtension(document.Content, documentDto.Extension);
				var estimation = Math.Round((await _processingTimeEstimator.EstimateTime(fileLength) / 1000.0), 2);

				if(!await _documentProcessingService.ProcessDocument(new DocumentInfo()
				{
				FileName = document.FileName,
				Extension = document.Extension.ToString().ToLower(),
				Version = version,
				CourseId = Guid.Parse(courseId),
				}, document.UserId))
				{
					return (false, 0);
				}

				var isSuccesfull = await _documentService.UpdateRateLimit(document.UserId);

				return (isSuccesfull, estimation);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<bool> DeleteDocument(Guid userId, string fileName)
		{
			try
			{
				if (await _documentService.DeleteDocument(userId, fileName))
				{
					return await _analysisService.DeleteAnalysis(userId, fileName);
				}

				return false;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<(List<DocumentInfo>, int)> GetDocumentsForStudent(Guid userId, int pageNumber, int pageSize)
		{
			try
			{
				var (documents, numOfDocs) = await _documentService.GetDocumentsPaged(userId, pageNumber, pageSize );

				foreach (var item in documents)
				{
					var course = await _courseService.GetCourseById(item.CourseId.ToString());

					item.CourseName = course.Title;
				}

				return (documents, numOfDocs);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<(byte[]? content, string contentType)> DownloadDocument(DocumentInfo document, Guid userId)
		{
			try
			{
				return await _documentService.DownloadDocument(document, userId);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
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
			var config = new MapperConfiguration(cfg =>
			{
				cfg.AddProfile(new MappingProfile());
			});

			_mapper = config.CreateMapper();

			Guard.EnsureNotNull(_mapper, nameof(_mapper));

			_documentService = await _factory.CreateServiceProxyAsync<IDocument>(ApiRoutes.DocumentService, true);
			_documentProcessingService = await _factory.CreateServiceProxyAsync<IDocumentProcessing>(ApiRoutes.DocumentProcessingService, true);
			_analysisService = await _factory.CreateServiceProxyAsync<IAnalysis>(ApiRoutes.AnalysisService, true);
			_processingTimeEstimator = await _factory.CreateServiceProxyAsync<IProcessingTimeEstimator>(ApiRoutes.ProcessingTimeEstimatorService, true);
			_courseService = await _factory.CreateServiceProxyAsync<ICourse>(ApiRoutes.CourseService, true);
			_userService = await _factory.CreateServiceProxyAsync<IUser>(ApiRoutes.UserService, true);

			Guard.EnsureNotNull(_courseService, nameof(_courseService));
			Guard.EnsureNotNull(_analysisService, nameof(_analysisService));
			Guard.EnsureNotNull(_documentService, nameof(_documentService));
			Guard.EnsureNotNull(_documentProcessingService, nameof(_documentProcessingService));
			Guard.EnsureNotNull(_processingTimeEstimator, nameof(_processingTimeEstimator));
		}
	}
}
