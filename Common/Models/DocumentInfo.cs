
namespace Common.Models
{
	public class DocumentInfo
	{
		public string FileName { get; set; }
		public string Extension { get; set; }
		public int Version { get; set; }
		public string CourseName { get; set; }
		public Guid UserId { get; set; }
		public Guid CourseId { get; set; }
	}
}
