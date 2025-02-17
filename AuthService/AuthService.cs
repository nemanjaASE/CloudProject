using System.ComponentModel.DataAnnotations;
using System.Fabric;
using Common.Constants;
using Common.DTO;
using Common.Guard;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace AuthService
{
	public class AuthService(StatelessServiceContext context) : StatelessService(context), IAuth
    {
		private IUser? _userService;
		private readonly ServiceClientFactory _factory = new();

		public async Task<LoggedUserDTO?> Login(UserLoginDTO user)
		{
			if (user == null) { return null; }

			if (user.Password is null || user.Email is null) { return null; }

			if (!new EmailAddressAttribute().IsValid(user.Email)) { return null; }

			try
			{
				User retrievedUser = await _userService.GetUserByEmail(user.Email);

				if (retrievedUser is null) { return null; }

				if(!PasswordHasher.VerifyPassword(user.Password, retrievedUser.Password))
				{
					return null;
				}

				return new LoggedUserDTO()
				{
					Role = retrievedUser.Role,
					UserId = retrievedUser.Id,
				};
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<bool> ChangePassword(Guid userId, string newPassword)
		{
			try
			{
				var user = await _userService.GetUserById(userId);

				if (user is null) { return false; }

				user.Password = PasswordHasher.HashPassword(newPassword);

				return await _userService.UpdateUser(user);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}

		protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
			return
			[
				new(serviceContext =>
					new FabricTransportServiceRemotingListener(
						serviceContext,
						this,
						new FabricTransportRemotingListenerSettings
							{
								ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.Default,
							}),
						"ServiceEndpointV2")
			];
		}

		protected override async Task RunAsync(CancellationToken cancellationToken)
		{
			_userService = await _factory.CreateServiceProxyAsync<IUser>(ApiRoutes.UserService, true);
			Guard.EnsureNotNull(_userService, nameof(_userService));
		}
	}
}
