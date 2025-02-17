using System.Fabric;
using AutoMapper;
using Common.Constants;
using Common.DTO;
using Common.Entities;
using Common.Enums;
using Common.Guard;
using Common.Interfaces;
using Common.Mappers;
using Common.Models;
using Common.Repositories;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace AnalysisService
{
	internal sealed class AnalysisService(StatefulServiceContext context) : StatefulService(context), IAnalysis
	{
		private ITableRepository<AnalysisEntity>? _tableRepository;
		private IMapper? _mapper;
		private const string tableName = ConfigKeys.AnalysisTableName;

		private IReliableDictionary<Guid, List<AnalysisEntity>>? _analysis;
		private const string ANALYSIS_DICTIONARY = ConfigKeys.ReliableDictionaryAnalysis;

		#region DELETE
		public async Task<bool> DeleteAnalysis(Guid userId, string fileName)
		{

			try
			{
				if (userId.Equals(null) || fileName.Equals(string.Empty))
				{
					return false;
				}

				var userAnalysis = await _tableRepository.QueryAsync(tableName);

				foreach (var analysis in userAnalysis)
				{
					if (analysis.PartitionKey.Equals(userId.ToString()) && analysis.RowKey.StartsWith(fileName))
					{
						await _tableRepository.DeleteAsync(tableName, userId.ToString(), analysis.RowKey);
					}
				}

				_analysis = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, List<AnalysisEntity>>>(ANALYSIS_DICTIONARY);
				using var tx = StateManager.CreateTransaction();

				var result = await _analysis.TryGetValueAsync(tx, userId);
				if (!result.HasValue)
				{
					return false;
				}

				var updatedAnalysis = result.Value.Where(a => !a.RowKey.StartsWith(fileName)).ToList();

				if (updatedAnalysis.Count == result.Value.Count)
				{
					return false;
				}

				await _analysis.SetAsync(tx, userId, updatedAnalysis);
				await tx.CommitAsync();

				return true;
			}
			catch (Exception)
			{
				throw;
			}
		}
		#endregion
		#region POST
		public async Task<bool> AddAnalysis(Analysis analysis)
		{
			try
			{
				var analysisEntity = _mapper.Map<AnalysisEntity>(analysis);

				analysisEntity.PartitionKey = analysis.UserId.ToString();
				analysisEntity.RowKey = analysis.FileName;
				analysisEntity.Status = analysis.Status.ToString();

				await _tableRepository.InsertOrMergeAsync(tableName, analysisEntity);

				using var tx = StateManager.CreateTransaction();
				_analysis = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, List<AnalysisEntity>>> (ANALYSIS_DICTIONARY);

				var existingAnalyses = await _analysis.TryGetValueAsync(tx, analysis.UserId);

				List<AnalysisEntity> updatedAnalysis = existingAnalyses.HasValue
					? new List<AnalysisEntity> (existingAnalyses.Value) { analysisEntity }
					: [analysisEntity];

				await _analysis.SetAsync(tx, Guid.Parse(analysisEntity.PartitionKey), updatedAnalysis);
				
				await tx.CommitAsync();

				return true;
			}
			catch (Exception)
			{
				throw;
			}
		}
		#endregion
		#region PUT
		public async Task<bool> UpdateAnalysis(Analysis analysis)
		{
			try
			{
				var existingEntity = await _tableRepository.RetrieveAsync(tableName, analysis.UserId.ToString(), analysis.FileName);
				
				if (existingEntity is null) { return false; }

				existingEntity.PotentialImprovements = analysis.PotentialImprovements
					.Select(i => new Common.Entities.Improvement { Location = i.Location, Suggestion = i.Suggestion })
					.ToList();

				existingEntity.References = analysis.References
					.Select(i => new Common.Entities.Reference { Title = i.Title, Author = i.Author })
					.ToList();

				existingEntity.Score = analysis.Score;
				existingEntity.Status = analysis.Status.ToString();
				existingEntity.ProcessTimeS = analysis.ProcessTimeS;

				await _tableRepository.InsertOrMergeAsync(tableName, existingEntity);

				using var tx = StateManager.CreateTransaction();
				_analysis = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, List<AnalysisEntity>>>(ANALYSIS_DICTIONARY);

				var existingAnalyses = await _analysis.TryGetValueAsync(tx, analysis.UserId);

				if(!existingAnalyses.HasValue) { return false; }

				var analysesList = new List<AnalysisEntity>(existingAnalyses.Value);
				var index = analysesList.FindIndex(a => a.RowKey.Equals(analysis.FileName));

				if (index.Equals(-1)) { return false; }

				analysesList[index] = existingEntity;

				await _analysis.SetAsync(tx, analysis.UserId, analysesList);
				await tx.CommitAsync();

				return true;
			}
			catch (Exception)
			{
				throw;
			}
		}
		#endregion
		#region GET
		public async Task<int> GetNumOfDocuments(Guid userId, AnalysisStatus status)
		{
			try
			{
				_analysis = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, List<AnalysisEntity>>>(ANALYSIS_DICTIONARY);
				using var tx = StateManager.CreateTransaction();

				var analyses = await _analysis.TryGetValueAsync(tx, userId);

				if (!analyses.HasValue)
				{
					return 0;
				}

				return analyses.Value.Where(a => a.Status.Equals(status.ToString())).ToList().Count;
	
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<List<Progress>> GetAllAnalysesForUser(Guid userId)
		{
			try
			{
				_analysis = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, List<AnalysisEntity>>> (ANALYSIS_DICTIONARY);
				using var tx = StateManager.CreateTransaction();

				var analyses = await _analysis.TryGetValueAsync(tx, userId);

				if (!analyses.HasValue)
				{
					return [];
				}

				var retVal = new List<Progress>();

				var selectedAnalyses = analyses.Value.Where(a => a.Status.Equals(AnalysisStatus.ANALYZED.ToString()))
													 .OrderBy(a => a.Timestamp).ToList();

				selectedAnalyses.ForEach(a => retVal.Add(new Progress()
				{
					AnalysisDate = a.Timestamp.UtcDateTime.ToLocalTime(),
					Score = a.Score,
					SuggestionCount = a.PotentialImprovements.Count,
					DocumentVersion = GetDocumentVersion(a.RowKey),
					FileName = a.RowKey.ToString(),
					CourseId = a.CourseId.ToString(),
				}));

				return retVal;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<AnalysisDTO?> GetAnalysis(Guid userId, string fileName)
		{
			try
			{
				_analysis = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, List<AnalysisEntity>>>(ANALYSIS_DICTIONARY);
				using var tx = StateManager.CreateTransaction();

				var result = await _analysis.TryGetValueAsync(tx, userId);

				if (!result.HasValue)
				{
					return null;
				}

				foreach (var item in result.Value)
				{
					if (fileName.Equals(item.RowKey))
					{
						var potentialImprovements = new List<Common.Models.Improvement>();
						item.PotentialImprovements.ForEach(i => potentialImprovements.Add(new Common.Models.Improvement()
						{
							Location = i.Location,
							Suggestion = i.Suggestion,
						}));

						var references = new List<Common.Models.Reference>();
						item.References.ForEach(i => references.Add(new Common.Models.Reference()
						{
							Title = i.Title,
							Author = i.Author,
						}));

						Enum.TryParse(item.Status, true, out AnalysisStatus status);

						return new AnalysisDTO() 
						{
							References = references,
							PotentialImprovements = potentialImprovements,
							Score = item.Score,
							Status = status,
							DateTime = item.Timestamp.UtcDateTime.ToLocalTime(),
							ProcessingTimeS = item.ProcessTimeS,
						};
					}
				}

				return null;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<List<Progress>> GetProgress(Guid userId, string fileName)
		{
			try
			{
				_analysis = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, List<AnalysisEntity>>>(ANALYSIS_DICTIONARY);
				using var tx = StateManager.CreateTransaction();

				var analyses = await _analysis.TryGetValueAsync(tx, userId);

				if (!analyses.HasValue)
				{
					return [];
				}

				var retVal = new List<Progress>();

				foreach (var item in analyses.Value)
				{
					if (item.Status.Equals(AnalysisStatus.ANALYZED.ToString()) && item.RowKey.StartsWith(fileName))
					{
						List<Common.Models.Improvement> improvements = [];

						item.PotentialImprovements.ForEach(i => improvements.Add(new Common.Models.Improvement()
						{
							Location = i.Location,
							Suggestion = i.Suggestion,
						}));

						retVal.Add(new Progress()
						{
							AnalysisDate = item.Timestamp.UtcDateTime.ToLocalTime(),
							Score = item.Score,
							SuggestionCount = item.PotentialImprovements.Count,
							DocumentVersion = GetDocumentVersion(item.RowKey),
							Improvements = improvements,
						});
					}
				}

				return [.. retVal.OrderBy(a => a.AnalysisDate)]; ;
			}
			catch (Exception)
			{
				throw;
			}
		}
		#endregion

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
			var storageConnectionString = Environment.GetEnvironmentVariable(ConfigKeys.ConnectionString) ?? "UseDevelopmentStorage=true";

			_tableRepository = new TableRepository<AnalysisEntity>(storageConnectionString);

			Guard.EnsureNotNull(_tableRepository, nameof(_tableRepository));

			var config = new MapperConfiguration(cfg =>
			{
				cfg.AddProfile(new MappingProfile());
			});

			_mapper = config.CreateMapper();

			await LoadAnalyses();
		}

		private async Task LoadAnalyses()
		{
			_analysis = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, List<AnalysisEntity>>>(ANALYSIS_DICTIONARY);

			try
			{
				using var tx = StateManager.CreateTransaction();
				Dictionary<string, List<AnalysisEntity>> analyses = [];

				var result = await _tableRepository.QueryAsync(tableName);

				foreach (var analysis in result)
				{
					if (!analyses.TryGetValue(analysis.PartitionKey, out List<AnalysisEntity>? value))
					{
						analyses.Add(analysis.PartitionKey, [analysis]);
					}
					else
					{
						value.Add(analysis);
					}
				}

				foreach (var a in analyses)
				{
					await _analysis.AddAsync(tx, Guid.Parse(a.Key), a.Value);
				}

				await tx.CommitAsync();
			}
			catch (Exception)
			{
				throw;
			}
		}
		private static int GetDocumentVersion(string filename)
		{
			return int.Parse(filename.Split('.')[0].Split('_')[1]);
		}
	}
}
