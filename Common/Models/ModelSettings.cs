
namespace Common.Models
{
	public class ModelSettings
	{
		public string ModelName { get; set; }
		public double Temperature { get; set; }
		public int MaxTokens { get; set; }
		public List<string>? AdditionalRequirements { get; set; }
	}
}
