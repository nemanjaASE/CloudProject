using System.Fabric;
using Common.Constants;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;


namespace ProcessingTimeEstimator
{
    internal sealed class ProcessingTimeEstimator(StatefulServiceContext context) : StatefulService(context), IProcessingTimeEstimator
    {
		private IReliableDictionary<int, double>? _history;
		private const string HISTORY_DICTIONARY = ConfigKeys.ReliableDictionaryHistory;

		private readonly int OverheadMs = 300; // Network Latency [ms]
		private readonly int TPS = 16; // Token Per Second [s]
		private readonly int CharsInToken = 4; // 4 Characters In One Token

		public async Task<double> EstimateTime(int textLength)
		{
			if (await HistoryIsEmpty())
			{
				return SimpleEstimate(textLength);
			}

			_history = await StateManager.GetOrAddAsync<IReliableDictionary<int, double>>(HISTORY_DICTIONARY);

			List<ProcessingData> _processingData = [];

			using var tx = StateManager.CreateTransaction();
			var enumerable = await _history.CreateEnumerableAsync(tx);

			using var enumerator = enumerable.GetAsyncEnumerator();
			while (await enumerator.MoveNextAsync(default(CancellationToken)))
			{
				_processingData.Add(new ProcessingData()
				{
					TextLength = enumerator.Current.Key,
					ProcessingTimeMs = enumerator.Current.Value,
				});
			}

			var similarEntries = _processingData.Where(entry => Math.Abs(entry.TextLength - textLength) < 50).ToList();

			if (similarEntries.Count != 0)
			{
				double avgTime = similarEntries.Average(entry => entry.ProcessingTimeMs);
				return avgTime;
			}

			return SimpleEstimate(textLength);
		}
		private int CountTokens(int textLength)
		{
			return textLength / CharsInToken;
		}
		private double SimpleEstimate(int textLength)
		{
			int tokenCount = CountTokens(textLength);
			double estimatedTimeSeconds = (double)tokenCount / TPS;
			return (estimatedTimeSeconds * 1000) + OverheadMs; // Convert To [ms]
		}
		public async Task LogProcessingTime(int textLength, double time)
		{
			using var tx = StateManager.CreateTransaction();
			_history = await StateManager.GetOrAddAsync<IReliableDictionary<int, double>>(HISTORY_DICTIONARY);

			await _history.SetAsync(tx, textLength, time);
			await tx.CommitAsync();
		}
		private async Task<int> HistoryCounter()
		{
			_history = await StateManager.GetOrAddAsync<IReliableDictionary<int, double>>(HISTORY_DICTIONARY);

			using var tx = StateManager.CreateTransaction();
			var enumerable = await _history.CreateEnumerableAsync(tx);
			int counter = 0;
			using var enumerator = enumerable.GetAsyncEnumerator();
			while (await enumerator.MoveNextAsync(default(CancellationToken)))
			{
				counter++;
			}

			return counter;
		}

        private async Task<bool> HistoryIsEmpty()
        {
            _history = await StateManager.GetOrAddAsync<IReliableDictionary<int, double>>(HISTORY_DICTIONARY);

            using var tx = StateManager.CreateTransaction();
            var enumerable = await _history.CreateEnumerableAsync(tx);
            using var enumerator = enumerable.GetAsyncEnumerator();

            return !await enumerator.MoveNextAsync(default);
        }
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
			return
			[
				new ServiceReplicaListener(serviceContext =>
					new FabricTransportServiceRemotingListener(
						serviceContext,
						this, new FabricTransportRemotingListenerSettings
							{
								ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.Default,
							})
					)
			];
		}

		protected override async Task RunAsync(CancellationToken cancellationToken)
		{
			_history = await StateManager.GetOrAddAsync<IReliableDictionary<int, double>>(HISTORY_DICTIONARY);
		}
	}
}
