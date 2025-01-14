using Common.Enums;
using System.ComponentModel.DataAnnotations;

namespace WebApp.Models
{
	public class UserViewModel
	{
		public Guid Id { get; set; }

		[Required(ErrorMessage = "First name is required.")]
		[MaxLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
		public string FirstName { get; set; }

		[Required(ErrorMessage = "Last name is required.")]
		[MaxLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
		public string LastName { get; set; }

		[Required(ErrorMessage = "Email is required.")]
		[EmailAddress(ErrorMessage = "Invalid email format.")]
		public string Email { get; set; }

		[Required(ErrorMessage = "Role is required.")]
		public UserRole Role { get; set; }
	}
}
