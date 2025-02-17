
namespace Common.Models
{
	public class Progress
	{
		public string FileName { get; set; }
		public string CourseId { get; set; }
		public string CourseName { get; set; }
		public string AuthorName { get; set; }
		public DateTime AnalysisDate { get; set; }
		public int Score { get; set; }
		public int DocumentVersion { get; set; }
		public int SuggestionCount { get; set; }
		public List<Improvement> Improvements { get; set; }
	}
}
