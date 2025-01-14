using System;
using System.ComponentModel.DataAnnotations;
using System.Fabric;
using Common.DTO;
using Common.Enums;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace AuthService
{
	public class AuthService : StatelessService, IAuth
    {
		private readonly IUser _userService;
        public AuthService(StatelessServiceContext context)
            : base(context)
        {
			var serviceProxyFactory = new ServiceProxyFactory((callbackClient) =>
			{
				return new FabricTransportServiceRemotingClientFactory(
					new FabricTransportRemotingSettings
					{
						ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Default
					}, callbackClient);
			});

			var serviceUri = new Uri("fabric:/EduAnalyzer/UserService");

			_userService = serviceProxyFactory.CreateServiceProxy<IUser>(serviceUri, new ServicePartitionKey(0));
		}

		public async Task<LoggedUserDTO> Login(UserLoginDTO user)
		{
			if (user == null) { return null; }

			if (user.Password is null || user.Email is null) { return null; }

			if (!new EmailAddressAttribute().IsValid(user.Email)) { return null; }

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

		protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
			return new List<ServiceInstanceListener>
			{
				new ServiceInstanceListener(serviceContext =>
					new FabricTransportServiceRemotingListener(
						serviceContext,
						this,
						new FabricTransportRemotingListenerSettings
							{
								ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.Default,
							}),
						"ServiceEndpointV2")
			};
		}
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {

            long iterations = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
