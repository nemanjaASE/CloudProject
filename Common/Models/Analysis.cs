using Common.Enums;

namespace Common.Models
{
	public class Analysis
	{
		public Guid UserId { get; set; }
		public string FileName { get; set; }
		public List<Improvement> PotentialImprovements { get; set; }
		public List<Reference> References { get; set; }
		public int Score { get; set; }
		public AnalysisStatus Status { get; set; }
		public double ProcessTimeS { get; set; }
		public Guid CourseId { get; set; }
	}
}
