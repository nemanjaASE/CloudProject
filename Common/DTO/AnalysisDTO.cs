using Common.Enums;
using Common.Models;

namespace Common.DTO
{
	public class AnalysisDTO
	{
		public List<Improvement> PotentialImprovements { get; set; }
		public List<Reference> References { get; set; }
		public int Score { get; set; }
		public AnalysisStatus Status { get; set; }
		public DateTime DateTime { get; set; }
		public double ProcessingTimeS { get; set; }
	}
}
