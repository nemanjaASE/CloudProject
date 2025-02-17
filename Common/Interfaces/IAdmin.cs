using Common.DTO;
using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface IAdmin : IService
	{
		Task<List<User>> GetAllUsers();
		Task<(List<User>, int)> GetUsersPaged(int page, int pageSize);
		Task<User?> GetUserById(Guid id);
		Task<bool> DeleteUser(Guid id);
		Task<bool> UpdateUser(User user);
		Task<bool> AddNewUser(User user);
		Task<bool> UpdateSettings(Settings settings);
		Task<Settings?> GetSettings();
		Task<ModelListResponse?> GetActiveModels();
		Task<(List<DocumentViewDTO>, int)> GetAllDocumentsPaged(int pageNumber, int pageSize);
		Task<AnalysisDTO?> GetAnalysis(Guid userId, string fileName);
		Task<ProgressDTO?> GetProgress(Guid userId, string fileName);
		Task<(byte[]? content, string contentType)> DownloadDocument(DocumentInfo document, Guid studentId);
		Task<bool> DeleteDocument(Guid studentId, string fileName);
		Task<bool> ChangePassword(Guid userId, string newPassword);
	}
}
