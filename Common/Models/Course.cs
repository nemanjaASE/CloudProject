
namespace Common.Models
{
	public class Course
	{
		public string Title { get; set; }
		public string Description { get; set; }
		public Guid CourseId { get; set; }
		public Guid AuthorId { get; set; }
		public string AuthorName { get; set; }
		public DateTime CreatedDate { get; set; }
	}
}
