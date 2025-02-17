using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using System.Fabric;

namespace Common.Helpers
{
	public class ServiceClientFactory
	{
		private readonly ServiceProxyFactory _serviceProxyFactory;
		private static readonly Random _random = new();

		public ServiceClientFactory()
		{
			_serviceProxyFactory = new ServiceProxyFactory((callbackClient) =>
				new FabricTransportServiceRemotingClientFactory(
					new FabricTransportRemotingSettings
					{
						ExceptionDeserializationTechnique = FabricTransportRemotingSettings.ExceptionDeserialization.Default
					}, callbackClient));
		}

		public async Task<T> CreateServiceProxyAsync<T>(string serviceName, bool isStateful) where T : IService
		{
			var serviceUri = new Uri($"{Constants.ApiRoutes.BaseRoute}{serviceName}");

			if (isStateful)
			{
				long partitionKey = await GetRandomPartitionKey(serviceUri);
				return _serviceProxyFactory.CreateServiceProxy<T>(serviceUri, new ServicePartitionKey(partitionKey));
			}
			else
			{
				return _serviceProxyFactory.CreateServiceProxy<T>(serviceUri);
			}
		}

		private async Task<long> GetRandomPartitionKey(Uri serviceUri)
		{
			using var fabricClient = new FabricClient();
			var partitions = await fabricClient.QueryManager.GetPartitionListAsync(serviceUri);

			if (partitions.Count == 0)
				throw new InvalidOperationException("No partitions found for the service.");

			var randomPartition = partitions[_random.Next(partitions.Count)];
			return ((Int64RangePartitionInformation)randomPartition.PartitionInformation).LowKey;
		}
	}
}
