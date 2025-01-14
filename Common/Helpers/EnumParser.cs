using Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Helpers
{
	public class EnumParser
	{
		public static UserRole GetUserRoleTypeFromString(string role)
		{
			Enum.TryParse(role, ignoreCase: true, out UserRole userRoleType);
			return userRoleType;
		}
	}
}
