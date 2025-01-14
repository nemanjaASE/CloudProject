using Common.Enums;

namespace Common.DTO
{
	public class LoggedUserDTO
	{
		public UserRole Role {  get; set; }
		public Guid UserId { get; set; }
	}
}
