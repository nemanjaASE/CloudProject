namespace WebApp.Models
{
	using Microsoft.AspNetCore.Mvc.Rendering;
	using System.ComponentModel.DataAnnotations;

	public class CourseFilterViewModel
	{
		public DateTime? StartDate { get; set; }

		public DateTime? EndDate { get; set; }

		public string? SelectedCourse { get; set; }

		public List<SelectListItem>? Courses { get; set; }
	}
}
