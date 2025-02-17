using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface IDocumentProcessing : IService
	{
		Task<string> AnalyzeSuggestions(List<Improvement> improvements);
		Task<ModelSettings?> GetSettings();
		Task<bool> ProcessDocument(DocumentInfo documentInfo, Guid userId);
		Task<bool> UpdateSettings(ModelSettings settings);
	}
}
