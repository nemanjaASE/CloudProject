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
		Task<int> FindLatestVersion(Guid userId, string fileName);
		Task<bool> DeleteDocument(Guid userId, string fileName);
		Task<bool> DeleteSpecificDocumentVersion(Guid userId, string fileName, int version);
	}
}
