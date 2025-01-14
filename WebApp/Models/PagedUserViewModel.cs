namespace WebApp.Models
{
	public class PagedUserViewModel
	{
		public IEnumerable<UserViewModel> Users { get; set; }
		public int Page { get; set; }
		public int PageSize { get; set; }
		public int TotalCount { get; set; }

		public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
	}
}
