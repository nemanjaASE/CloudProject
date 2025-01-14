using System.ComponentModel.DataAnnotations;

namespace WebApp.Models
{
	public class NewUserViewModel
	{

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
		public string Role { get; set; }

		[Required(ErrorMessage = "Password is required.")]
		public string Password { get; set; }
	}
}
