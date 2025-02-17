using Common.DTO;
using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface IProfessor : IService
	{
		Task<(List<User>, int)> GetStudentsPaged(int page, int pageSize);
		Task<List<DocumentInfo>> GetDocumentsForStudent(Guid professorId, Guid studentId);
		Task<AnalysisDTO?> GetAnalysis(Guid userId, string fileName);
		Task<ProgressDTO?> GetProgress(Guid userId, string fileName);
		Task<(List<Course>, int)> GetCoursesPaged(int page, int pageSize, Guid authorId);
		Task<(byte[]? content, string contentType)> DownloadDocument(DocumentInfo document, Guid userId);
		Task<bool> UpdateAnalysisReference(string title, string author, Guid userId, string fileName);
		Task<bool> UpdateAnalysisSuggestion(string suggestion, string location, Guid userId, string fileName);
		Task<bool> AddNewCourse(string title, string description, Guid authorId);
		Task<byte[]> GenerateReportPdf(Guid professorId, DateTime? startDate, DateTime? endDate, string? courseId);
		Task<List<Course>> GetCoursesForProfessor(Guid professorId);
		Task<Course?> GetCourse(Guid professorId, string courseId);
		Task<bool> UpdateCourse(Course updatedCourse);
		Task<bool> DeleteCourse(Guid courseId, Guid authorId);
	}
}
