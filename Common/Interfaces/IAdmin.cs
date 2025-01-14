using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Interfaces
{
	public interface IAdmin : IService
	{
		Task<List<User>> GetAllUsers();
		Task<(List<User>, int)> GetUsersPaged(int page, int pageSize);
		Task<User> GetUserById(Guid id);
		Task<bool> DeleteUser(Guid id);
		Task<bool> UpdateUser(User user);
		Task<bool> AddNewUser(User user);
	}
}
