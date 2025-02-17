using Common.DTO;
using Common.Enums;
using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface IAnalysis : IService
	{
		Task<bool> AddAnalysis(Analysis analysis);
		Task<bool> UpdateAnalysis(Analysis analysis);
		Task<AnalysisDTO?> GetAnalysis(Guid userId, string fileName);
		Task<bool> DeleteAnalysis(Guid userId, string fileName);
		Task<List<Progress>> GetProgress(Guid userId, string fileName);
		Task<List<Progress>> GetAllAnalysesForUser(Guid userId);
		Task<int> GetNumOfDocuments(Guid userId, AnalysisStatus status);
	}
}
