using Common.Models;

namespace WebApp.Models
{
	public class PagedCourseViewModel : Pagination
	{
		public IEnumerable<CourseViewModel> Courses { get; set; }
	}
}
