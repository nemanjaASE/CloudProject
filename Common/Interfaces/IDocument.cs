using Microsoft.ServiceFabric.Services.Remoting;
using Common.Models;

namespace Common.Interfaces
{
	public interface IDocument : IService
	{
		Task<List<DocumentInfo>> GetDocumentsByUserId(Guid userId);
		Task<bool> UploadDocumentAsync(Document document);
		Task<(byte[] Content, string ContentType)> DownloadDocument(DocumentInfo document, Guid userId);
		Task<bool> UploadNewVersionAsync(Document document);
		Task<(int, string)> FindLatestVersion(Guid userId, string fileName);
		Task<bool> DeleteDocument(Guid userId, string fileName);
		Task<bool> DeleteSpecificDocumentVersion(Guid userId, string fileName, int version);
		Task<bool> DeleteAllDocumentsForUser(Guid userId);
		Task<bool> CheckIfFileExist(Guid userId, string fileName);
		Task<bool> CheckRateLimit(Guid userId);
		Task<bool> UpdateRateLimit(Guid userId);
		Task<RateLimit?> GetRateLimitSettings();
		Task<bool> SetRateLimitSettings(uint maxAttempts, uint timeInterval);
		Task<List<DocumentInfo>> GetDocumentsByCourses(List<string> courseNames);
		Task<(List<DocumentInfo>, int)> GetDocumentsPaged(Guid userId, int pageNumber, int pageSize);
		Task<(List<DocumentInfo>, int)> GetAllDocumentsPaged(int pageNumber, int pageSize);
	}
}
