
using Microsoft.WindowsAzure.Storage.Table;

namespace Common.Entities
{
	public class CourseEntity : TableEntity
	{
		public string Title { get; set; }
		public string Description { get; set; }
		public DateTime CreatedDate { get; set; }
	}
}
