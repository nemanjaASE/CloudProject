using Common.DTO;

namespace WebApp.Models
{
	public class DocumentViewModel
	{
		public string FileName { get; set; }
		public string Extension { get; set; }
		public int Version { get; set; }
		public string CourseName { get; set; }
		public Guid StudentId { get; set; }
		public string StudentFirstName { get; set; }
		public string StudentLastName { get; set; }
		public AnalysisDTO Analysis { get; set; }
		public ProgressDTO ProgressView { get; set; }
	}
}
