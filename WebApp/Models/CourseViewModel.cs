namespace WebApp.Models
{
	public class CourseViewModel
	{
		public string Title { get; set; }
		public string Description { get; set; }
		public DateTime CreatedDate { get; set; }
		public Guid AuthorId { get; set; }
		public Guid CourseId { get; set; }
	}
}
