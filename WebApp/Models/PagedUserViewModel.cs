using Common.Models;

namespace WebApp.Models
{
	public class PagedUserViewModel : Pagination
	{
		public IEnumerable<UserViewModel> Users { get; set; }
	}
}
