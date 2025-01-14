using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using Microsoft.WindowsAzure.Storage.Table;

namespace AdministratorService
{

	internal sealed class AdministratorService : StatelessService, IAdmin
    {
        private readonly IUser _userService;
        public AdministratorService(StatelessServiceContext context)
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

		public async Task<bool> AddNewUser(User user)
		{
			if(user is null)
			{
				return false;
			}

			if(await _userService.CheckIfUserExistsByEmail(user.Email))
			{
				return false;
			}

			return await _userService.AddNewUser(user);
		}

		public async Task<bool> DeleteUser(Guid id)
		{
			return await _userService.DeleteUser(id);
		}

		public async Task<List<User>> GetAllUsers()
		{
			return await _userService.GetAllUsers();
		}

		public async Task<User> GetUserById(Guid id)
		{
			return await _userService.GetUserById(id);
		}

		public async Task<(List<User>, int)> GetUsersPaged(int page, int pageSize)
		{
			return await _userService.GetUsersPaged(page, pageSize);
		}

		public async Task<bool> UpdateUser(User user)
		{
			if (user is null)
			{
				return false;
			}

			if (await _userService.CheckIfUserExistsById(user.Id))
			{
				return await _userService.UpdateUser(user);
			}

			return false;
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
