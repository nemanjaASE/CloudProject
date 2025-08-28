using System.Fabric;
using Common.Constants;
using Common.DTO;
using Common.Guard;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace ProfessorService
{
	internal sealed class ProfessorService(StatelessServiceContext context) : StatelessService(context), IProfessor
	{
		private IUser? _userService;
		private IDocument? _documentService;
		private IAnalysis? _analysisService;
		private ICourse? _courseService;
		private IDocumentProcessing? _documentProcessingService;
		private readonly ServiceClientFactory _factory = new();

		#region COURSE
		public async Task<Course?> GetCourse(Guid professorId, string courseId)
		{
			try
			{
				return await _courseService.GetCourse(professorId, courseId);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<bool> AddNewCourse(string title, string description, Guid authorId)
		{
			try
			{
				return await _courseService.AddCourse(new Course()
				{
					Title = title,
					Description = description,
					AuthorId = authorId,
					CreatedDate = DateTime.UtcNow,
				});
			}
			catch (Exception e)
			{

				throw new Exception(e.Message);
			}
		}
		public async Task<bool> UpdateCourse(Course updatedCourse)
		{
			try
			{
				return await _courseService.UpdateCourse(updatedCourse);
			}
			catch (Exception)
			{
				return false;
			}
		}
		public async Task<bool> DeleteCourse(Guid courseId, Guid authorId)
		{
			try
			{
				return await _courseService.DeleteCourse(courseId, authorId);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<List<Course>> GetCoursesForProfessor(Guid professorId)
		{
			try
			{
				return await _courseService.GetCoursesForProfessor(professorId);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<(List<Course>, int)> GetCoursesPaged(int page, int pageSize, Guid authorId)
		{
			try
			{
				return await _courseService.GetCoursesPaged(page, pageSize, authorId);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		#endregion
		#region STUDENT
		public async Task<(List<User>, int)> GetStudentsPaged(int page, int pageSize)
		{
			try
			{
				return await _userService.GetStudentsPaged(page, pageSize);
			}
			catch (Exception e)
			{

				throw new Exception(e.Message);
			}
		}
		public async Task<List<DocumentInfo>> GetDocumentsForStudent(Guid professorId, Guid studentId)
		{
			try
			{
				var courses = await _courseService.GetCoursesForProfessor(professorId);

				if (courses is null || courses.Count.Equals(0)) { return []; }

				List<string> coursesIds = [];
				courses.ForEach(c => coursesIds.Add(c.CourseId.ToString()));

				var documentInfos = await _documentService.GetDocumentsByUserId(studentId);

				if (documentInfos is null || documentInfos.Count.Equals(0)) { return []; }

				return documentInfos.Where(di => coursesIds.Contains(di.CourseId.ToString())).Select(di =>
                {
                    var course = courses.FirstOrDefault(c => c.CourseId == di.CourseId);
                    di.CourseName = course?.Title; 
                    return di;
                }).ToList();
			}
			catch (Exception)
			{

				throw;
			}
		}
		#endregion
		#region DOCUMENT
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
		#endregion
		#region PROGRESS
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
		public async Task<bool> UpdateAnalysisReference(string title, string author, Guid userId, string fileName)
		{
			try
			{
				var analysis = await _analysisService.GetAnalysis(userId, fileName);

				if (analysis is null)
				{
					return false;
				}

				analysis.References.Add(new Reference() { Author = author, Title = title });

				return await _analysisService.UpdateAnalysis(new Analysis()
				{
					FileName = fileName,
					Score = analysis.Score,
					Status = analysis.Status,
					ProcessTimeS = analysis.ProcessingTimeS,
					PotentialImprovements = analysis.PotentialImprovements,
					References = analysis.References,
					UserId = userId,
				});
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<bool> UpdateAnalysisSuggestion(string suggestion, string location, Guid userId, string fileName)
		{
			try
			{
				var analysis = await _analysisService.GetAnalysis(userId, fileName);

				if (analysis is null)
				{
					return false;
				}

				analysis.PotentialImprovements.Add(new Improvement() { Suggestion = suggestion, Location = location });

				return await _analysisService.UpdateAnalysis(new Analysis()
				{
					FileName = fileName,
					Score = analysis.Score,
					Status = analysis.Status,
					ProcessTimeS = analysis.ProcessingTimeS,
					PotentialImprovements = analysis.PotentialImprovements,
					References = analysis.References,
					UserId = userId,
				});
			}
			catch (Exception e)
			{

				throw new Exception(e.Message);
			}
		}
		#endregion
		#region REPORT
		public async Task<byte[]> GenerateReportPdf(Guid professorId, DateTime? startDate, DateTime? endDate, string? courseId)
		{
			try
			{
				var documentInfos = new List<DocumentInfo>();

				if (courseId is null)
				{
					var courses = await _courseService.GetCoursesForProfessor(professorId);
					documentInfos = await _documentService.GetDocumentsByCourses(courses.Select(c => c.CourseId.ToString()).ToList());
				}
				else
				{
					documentInfos = await _documentService.GetDocumentsByCourses([courseId]);
				}

				if (documentInfos is null || documentInfos.Count.Equals(0)) { return []; }

				List<StudentProgress> studentProgress = [];

				List<Improvement> allImprovements = [];

				foreach (var document in documentInfos)
				{
					var temp = await _analysisService.GetProgress(document.UserId, document.FileName);

					if (temp is null || temp.Count.Equals(0))
					{
						continue;
					}

					temp = temp.Where(p => (!startDate.HasValue || p.AnalysisDate >= startDate) && (!endDate.HasValue || p.AnalysisDate <= endDate)).ToList();

					var improvements = temp.Select(t => t.Improvements).FirstOrDefault();
					improvements.ForEach(i => allImprovements.Add(i));

					if (temp.Count.Equals(0)) { continue; }

					var user = await _userService.GetUserById(document.UserId);

					studentProgress.Add(new StudentProgress()
					{
						StudentFullName = $"{user.FirstName} {user.LastName}",
						FileName = document.FileName,
						CourseName = document.CourseName,
						Progresss = temp,
						AvgScore = Math.Round(temp.Average(p => p.Score))
					});
				}

				if (studentProgress is null || studentProgress.Count.Equals(0))
				{
					return [];
				}

				studentProgress = [.. studentProgress.OrderBy(sp => sp.CourseName).ThenBy(sp => sp.StudentFullName)];

				string commonMistakes = await _documentProcessingService.AnalyzeSuggestions(allImprovements);

				return FileHelper.GeneratePdf(studentProgress, startDate, endDate, commonMistakes);
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
			_analysisService = await _factory.CreateServiceProxyAsync<IAnalysis>(ApiRoutes.AnalysisService, true);
			_courseService = await _factory.CreateServiceProxyAsync<ICourse>(ApiRoutes.CourseService, true);
			_documentProcessingService = await _factory.CreateServiceProxyAsync<IDocumentProcessing>(ApiRoutes.DocumentProcessingService, true);

			Guard.EnsureNotNull(_courseService, nameof(_courseService));
			Guard.EnsureNotNull(_documentService, nameof(_documentService));
			Guard.EnsureNotNull(_analysisService, nameof(_analysisService));
			Guard.EnsureNotNull(_userService, nameof(_userService));
			Guard.EnsureNotNull(_documentProcessingService, nameof(_documentProcessingService));
		}
	}
}
