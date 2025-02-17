
using System.Text.Json.Serialization;

namespace Common.Models
{
	public class ModelInfo
	{
		public string OwnedBy { get; set; }

		public int ContextWindow { get; set; }
	}
	public class ModelListResponse
	{
		public Dictionary<string, ModelInfo> Data { get; set; }
	}
}
