using Microsoft.WindowsAzure.Storage.Table;

namespace Common.Entities
{
	public class UserEntity : TableEntity
	{
		public Guid Id { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string Password { get; set; }
	}
}
