using Common.Models;

namespace Common.DTO
{
	public class ProgressDTO
	{
		public List<Progress> Progress { get; set; }
		public int NumOfAnalyzed { get; set; }
		public int NumOfNotAnalyzed { get; set; }
		public int NumOfInProgress { get; set; }
		public int TotalDocuments { get; set; }
		public double AverageScore { get; set; }
	}
}
