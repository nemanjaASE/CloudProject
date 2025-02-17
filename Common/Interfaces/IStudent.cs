using Common.DTO;
using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface IStudent : IService
	{
		Task<(bool, double)> ProcessDocumentAsync(DocumentDTO documentDto);
		Task<(bool, double)> ProcessDocumentManually(DocumentDTO documentDto);
		Task<(List<DocumentInfo>, int)> GetDocumentsForStudent(Guid userId, int pageNumber, int pageSize);
		Task<(byte[]? content, string contentType)> DownloadDocument(DocumentInfo document, Guid userId);
		Task<(bool, double)> ProcessNewVersionAsync(DocumentDTO documentDto);
		Task<bool> DeleteDocument(Guid userId, string fileName);
		Task<bool> RollbackToPreviousVersionAsync(Guid userId, string fileName, int currentVersion);
		Task<AnalysisDTO?> GetAnalysis(Guid userId, string fileName);
		Task<ProgressDTO?> GetProgress(Guid userId, string fileName);
		Task<List<Course>> GetAllCourses();
		Task<ProgressDTO> GetProgressAllDocuments(Guid userId);
	}
}
