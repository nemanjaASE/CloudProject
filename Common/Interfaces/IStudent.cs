using Common.DTO;
using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface IStudent : IService
	{
		Task<bool> ProcessDocumentAsync(DocumentDTO document);
		Task<List<DocumentInfo>> GetDocumentsForStudent(Guid userId);
		Task<(byte[] content, string contentType)> DownloadDocument(DocumentInfo document, Guid userId);
		Task<bool> ProcessNewVersionAsync(DocumentDTO document);
		Task<bool> DeleteDocument(Guid userId, string fileName);
		Task<bool> RollbackToPreviousVersionAsync(Guid userId, string fileName);
	}
}
