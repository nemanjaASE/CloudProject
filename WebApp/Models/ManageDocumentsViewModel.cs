using Common.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace WebApp.Models
{
	public class ManageDocumentsViewModel : Pagination
	{
		public List<DocumentViewModel> Documents { get; set; } = [];
		public List<SelectListItem> Courses { get; set; } = [];
		public string SelectedCourse { get; set; }
	}

}
