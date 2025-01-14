using Common.DTO;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface IAuth : IService
	{
		Task<LoggedUserDTO> Login(UserLoginDTO user);
	}
}
