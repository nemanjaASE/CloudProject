using Common.DTO;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface IAuth : IService
	{
		Task<bool> ChangePassword(Guid userId, string newPassword);
		Task<LoggedUserDTO?> Login(UserLoginDTO user);
	}
}
