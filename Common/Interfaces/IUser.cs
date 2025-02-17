using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface IUser : IService
	{
		Task<bool> AddNewUser(User newUser);
		Task<bool> CheckIfUserExistsByEmail(string email);
		Task<bool> CheckIfUserExistsById(Guid id);
		Task<User> GetUserByEmail(string email);
		Task<User> GetUserById(Guid id);
		Task<List<User>> GetAllUsers();
		Task<bool> DeleteUser(Guid id);
		Task<bool> UpdateUser(User user);
		Task<(List<User>, int)> GetUsersPaged(int page, int pageSize);
		Task<(List<User>, int)> GetStudentsPaged(int page, int pageSize);
		Task<(string, string)> GetUserName(Guid id);
	}
}
