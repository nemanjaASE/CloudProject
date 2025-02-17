
namespace Common.Models
{
	public class StudentProgress
	{
		public string FileName { get; set; }
		public string StudentFullName { get; set; }
		public string CourseName { get; set; }
		public List<Progress> Progresss { get; set; }
		public double AvgScore { get; set; }
	}
}
