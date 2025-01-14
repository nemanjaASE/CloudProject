using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Helpers
{
	public class PasswordHasher
	{
		public static string HashPassword(string password, int workFactor = 12)
		{
			if (string.IsNullOrWhiteSpace(password))
			{
				throw new ArgumentException("Password cannot be null or empty.", nameof(password));
			}

			return BCrypt.Net.BCrypt.HashPassword(password, workFactor);
		}

		public static bool VerifyPassword(string password, string hashedPassword)
		{
			if (string.IsNullOrWhiteSpace(password))
			{
				throw new ArgumentException("Password cannot be null or empty.", nameof(password));
			}

			if (string.IsNullOrWhiteSpace(hashedPassword))
			{
				throw new ArgumentException("Hashed password cannot be null or empty.", nameof(hashedPassword));
			}

			return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
		}
	}
}
