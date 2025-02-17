using System.Fabric;
using Common.Constants;
using Common.Entities;
using Common.Guard;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using Common.Repositories;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace DocumentService
{
	internal sealed class DocumentService(StatefulServiceContext context) : StatefulService(context), IDocument
    {
		private IBlobRepository?_blobRepository;

		private IReliableDictionary<string, Dictionary<string,List<DocumentMetadata>>>? _documents;
		private IReliableDictionary<string, (int, DateTime)>? _rateLimitDictionary;
		private IReliableDictionary<string, RateLimit>? _rateLimitSettingsDictionary;

		private const string CONRAINER_NAME = ConfigKeys.ContainerBlobName;
		private const string RATE_LIMIT_DICTIONARY = ConfigKeys.ReliableDictionaryRateLimit;
		private const string DOCUMENT_DICTIONARY = ConfigKeys.ReliableDicitonaryDocument;
		private const string RATE_LIMIT_SETTINGS_DICTIONARY = ConfigKeys.ReliableDictionaryRateLimitSettings;

		#region SETTINGS
		public async Task<bool> SetRateLimitSettings(uint maxAttempts, uint timeInterval)
		{
			try
			{
				using var tx = StateManager.CreateTransaction();
				_rateLimitSettingsDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, RateLimit>>(RATE_LIMIT_SETTINGS_DICTIONARY);

				await _rateLimitSettingsDictionary.SetAsync(tx, RATE_LIMIT_SETTINGS_DICTIONARY, new RateLimit()
				{
					MaxAttempts = maxAttempts,
					TimeInterval = timeInterval,
				});
				await tx.CommitAsync();

				return true;
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<RateLimit?> GetRateLimitSettings()
		{
			try
			{
				using var tx = StateManager.CreateTransaction();
				_rateLimitSettingsDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, RateLimit>>(RATE_LIMIT_SETTINGS_DICTIONARY);

				var result = _rateLimitSettingsDictionary.TryGetValueAsync(tx, RATE_LIMIT_SETTINGS_DICTIONARY);

				if (result is null)
				{
					return null;
				}

				return result.Result.Value;
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<bool> UpdateRateLimit(Guid userId)
		{
			try
			{
				using var tx = StateManager.CreateTransaction();
				_rateLimitDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, (int, DateTime)>>(RATE_LIMIT_DICTIONARY);

				var tryGetResult = await _rateLimitDictionary.TryGetValueAsync(tx, userId.ToString());

				bool isSucessfull = false;

				if (tryGetResult.HasValue)
				{
					var (attempts, firstAttemptTime) = tryGetResult.Value;
					await _rateLimitDictionary.SetAsync(tx, userId.ToString(), (attempts + 1, firstAttemptTime));
					await tx.CommitAsync();

					isSucessfull = true;
				}
				else
				{
					isSucessfull = false;
				}

				return isSucessfull;
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<bool> CheckRateLimit(Guid userId)
		{
			try
			{
				using var tx = StateManager.CreateTransaction();
				_rateLimitDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, (int, DateTime)>>(RATE_LIMIT_DICTIONARY);
				_rateLimitSettingsDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, RateLimit>>(RATE_LIMIT_SETTINGS_DICTIONARY);

				var result = _rateLimitSettingsDictionary.TryGetValueAsync(tx, RATE_LIMIT_SETTINGS_DICTIONARY);

				if (result is null)
				{
					return false;
				}

				uint maxAttempts = result.Result.Value.MaxAttempts;
				uint timeInterval = result.Result.Value.TimeInterval;

				var tryGetResult = await _rateLimitDictionary.TryGetValueAsync(tx, userId.ToString());
				var now = DateTime.UtcNow;

				if (tryGetResult.HasValue)
				{
					var (attempts, firstAttemptTime) = tryGetResult.Value;
					var timeElapsed = now - firstAttemptTime;

					if (timeElapsed < TimeSpan.FromHours(timeInterval))
					{
						if (attempts >= maxAttempts)
						{
							return false;
						}
					}
					else
					{
						await _rateLimitDictionary.SetAsync(tx, userId.ToString(), (0, DateTime.UtcNow));
					}
				}
				else
				{
					await _rateLimitDictionary.SetAsync(tx, userId.ToString(), (0, DateTime.UtcNow));
				}
				await tx.CommitAsync();

				return true;
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		#endregion
		#region DOCUMENT
		public async Task<(byte[] Content, string ContentType)> DownloadDocument(DocumentInfo document, Guid userId)
		{
			try
			{
				var blobName = BlobHelper.CreateBlobName(userId.ToString(), document.FileName, document.Version, document.Extension);

				return await _blobRepository.DownloadFileAsBytesAsync(CONRAINER_NAME, blobName);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<bool> CheckIfFileExist(Guid userId, string fileName)
		{
			try
			{
				_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

				using var tx = StateManager.CreateTransaction();
				var result = await _documents.TryGetValueAsync(tx, userId.ToString());
				if (result.HasValue)
				{
					return result.Value.ContainsKey(fileName);
				}
				else
				{
					return false;
				}
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<List<DocumentInfo>> GetDocumentsByUserId(Guid userId)
		{
			try
			{
				_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

				using var tx = StateManager.CreateTransaction();
				var result = await _documents.TryGetValueAsync(tx, userId.ToString());
				if (result.HasValue)
				{
					List<DocumentInfo> documentInfos = [];

					foreach (var document in result.Value)
					{
						var latestDocument = document.Value.OrderByDescending(x => x.Version).FirstOrDefault();

						var parsedBlob = BlobHelper.ParseBlobName(latestDocument.BlobPath);

						documentInfos.Add(new DocumentInfo()
						{
							FileName = document.Key,
							Extension = parsedBlob[3],
							Version = Int32.Parse(parsedBlob[2]),
							CourseId = Guid.Parse(document.Value[0].CourseId),
						});
					}

					return documentInfos;
				}
				else
				{
					return null;
				}
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<(List<DocumentInfo>, int)> GetDocumentsPaged(Guid userId, int pageNumber, int pageSize)
		{
			try
			{
				_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

				using var tx = StateManager.CreateTransaction();
				var result = await _documents.TryGetValueAsync(tx, userId.ToString());

				if (!result.HasValue)
				{
					return ([], 0);
				}

				var allDocuments = result.Value
					.Select(document =>
					{
						var latestDocument = document.Value.OrderByDescending(x => x.Version).FirstOrDefault();
						var parsedBlob = BlobHelper.ParseBlobName(latestDocument.BlobPath);

						return new DocumentInfo
						{
							FileName = document.Key,
							Extension = parsedBlob[3],
							Version = int.Parse(parsedBlob[2]),
							CourseId = Guid.Parse(document.Value[0].CourseId),
						};
					})
					.OrderByDescending(d => d.Version)
					.ToList();

				int totalCount = allDocuments.Count;

				var paginatedDocuments = allDocuments
					.Skip((pageNumber - 1) * pageSize)
					.Take(pageSize)
					.ToList();

				return (paginatedDocuments, totalCount);
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<(List<DocumentInfo>, int)> GetAllDocumentsPaged(int pageNumber, int pageSize)
		{
			try
			{
				_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

				using var tx = StateManager.CreateTransaction();
				var allEntries = await _documents.CreateEnumerableAsync(tx);
				var enumerator = allEntries.GetAsyncEnumerator();

				List<DocumentInfo> allDocuments = [];

				while (await enumerator.MoveNextAsync(CancellationToken.None))
				{
					string key = enumerator.Current.Key;

					var userDocuments = enumerator.Current.Value;

					foreach (var document in userDocuments)
					{
						var latestDocument = document.Value.OrderByDescending(x => x.Version).FirstOrDefault();
						var parsedBlob = BlobHelper.ParseBlobName(latestDocument.BlobPath);

						allDocuments.Add(new DocumentInfo
						{
							FileName = document.Key,
							Extension = parsedBlob[3],
							Version = int.Parse(parsedBlob[2]),
							CourseId = Guid.Parse(document.Value[0].CourseId),
							UserId = Guid.Parse(key),
						});
					}
				}

				int totalCount = allDocuments.Count;

				var paginatedDocuments = allDocuments
					.OrderByDescending(d => d.Version)
					.Skip((pageNumber - 1) * pageSize)
					.Take(pageSize)
					.ToList();

				return (paginatedDocuments, totalCount);
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<(int, string)> FindLatestVersion(Guid userId, string fileName)
		{
			try
			{
				_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

				int latestVersion = 0;
				var courseId = String.Empty;
				using (var tx = StateManager.CreateTransaction())
				{
					var result = await _documents.TryGetValueAsync(tx, userId.ToString());

					if (result.HasValue)
					{
						var documents = result.Value;

						foreach (var document in documents)
						{
							if (document.Key.Equals(fileName))
							{
								var latestDocument = document.Value.OrderByDescending(x => x.Version).FirstOrDefault();

								latestVersion = latestDocument is not null ? latestDocument.Version : 0;
								courseId = latestDocument.CourseId;
								break;
							}
						}
					}
				}

				return (latestVersion, courseId);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<bool> UploadNewVersionAsync(Document document)
		{
			try
			{
				string blobName = BlobHelper.CreateBlobName(document.UserId.ToString(), document.FileName, document.Version, document.Extension);

				_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

				bool reliableDictionarySuccess = true;

				using (var tx = StateManager.CreateTransaction())
				{
					var documentMetadata = new DocumentMetadata()
					{
						Version = document.Version,
						BlobPath = blobName,
						CourseId = document.CourseId.ToString(),
					};

					var result = await _documents.TryGetValueAsync(tx, document.UserId.ToString());

					if (result.HasValue && result.Value.TryGetValue(document.FileName, out List<DocumentMetadata>? value))
					{
						value.Add(documentMetadata);
					}

					await tx.CommitAsync();

					reliableDictionarySuccess = true;
				}

				if (reliableDictionarySuccess)
				{
					await _blobRepository.UploadBlobAsync(CONRAINER_NAME, blobName, document.Content, document.CourseId.ToString(), document.ContentType);
				}

				return true;
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<bool> UploadDocumentAsync(Document document)
		{
			try
			{
				string blobName = BlobHelper.CreateBlobName(document.UserId.ToString(), document.FileName, document.Version, document.Extension);

				_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);
				bool reliableDictionarySuccess = true;

				using (var tx = StateManager.CreateTransaction())
				{
					var documentMetadata = new DocumentMetadata()
					{
						Version = document.Version,
						BlobPath = blobName,
						CourseId = document.CourseId.ToString(),
					};

					var result = await _documents.TryGetValueAsync(tx, document.UserId.ToString());

					if (result.HasValue)
					{
						result.Value.Add(document.FileName, [documentMetadata]);
					}
					else
					{
						await _documents.SetAsync(tx, document.UserId.ToString(), new Dictionary<string, List<DocumentMetadata>>()
					{
						{ document.FileName, new List<DocumentMetadata>() {documentMetadata} }
					});
					}

					await tx.CommitAsync();

					reliableDictionarySuccess = true;
				}

				if (reliableDictionarySuccess)
				{
					await _blobRepository.UploadBlobAsync(CONRAINER_NAME, blobName, document.Content, document.CourseId.ToString(), document.ContentType);
				}

				return true;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<bool> DeleteDocument(Guid userId, string fileName)
		{
			try
			{
				_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

				using var tx = StateManager.CreateTransaction();
				var result = await _documents.TryGetValueAsync(tx, userId.ToString());

				if (result.HasValue && result.Value.TryGetValue(fileName, out var metadataList) && metadataList is { Count: > 0 })
				{
					var rollbackData = new List<DocumentRollback>();

					try
					{
						foreach (var metadata in metadataList)
						{
							var (content, contentType) = await _blobRepository.DownloadFileAsBytesAsync(CONRAINER_NAME, metadata.BlobPath);
							await _blobRepository.DeleteBlobAsync(CONRAINER_NAME, metadata.BlobPath);
							rollbackData.Add(new DocumentRollback { BlobPath = metadata.BlobPath, Content = content, ContentType = contentType , CourseId = metadata.CourseId});
						}

						result.Value.Remove(fileName);
						await _documents.SetAsync(tx, userId.ToString(), result.Value);
						await tx.CommitAsync();

						return true;
					}
					catch (Exception)
					{
						foreach (var rollbackItem in rollbackData)
						{
							await _blobRepository.UploadBlobAsync(CONRAINER_NAME, rollbackItem.BlobPath, rollbackItem.Content, rollbackItem.CourseId, rollbackItem.ContentType);
						}

						return false;
					}
				}

				return false;
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<bool> DeleteAllDocumentsForUser(Guid userId)
		{
			try
			{
				_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

				using var tx = StateManager.CreateTransaction();
				var result = await _documents.TryGetValueAsync(tx, userId.ToString());

				if (!result.HasValue || result.Value.Count == 0)
				{
					return true;
				}

				var rollbackData = new List<DocumentRollback>();

				try
				{
					foreach (var doc in result.Value)
					{
						foreach (var metadata in doc.Value)
						{
							var (content, contentType) = await _blobRepository.DownloadFileAsBytesAsync(CONRAINER_NAME, metadata.BlobPath);
							await _blobRepository.DeleteBlobAsync(CONRAINER_NAME, metadata.BlobPath);
							rollbackData.Add(new DocumentRollback { BlobPath = metadata.BlobPath, Content = content, ContentType = contentType, CourseId = metadata.CourseId});
						}
					}

					result.Value.Remove(userId.ToString());
					await tx.CommitAsync();
				}
				catch (Exception)
				{
					foreach (var rollbackItem in rollbackData)
					{
						await _blobRepository.UploadBlobAsync(CONRAINER_NAME, rollbackItem.BlobPath, rollbackItem.Content, rollbackItem.CourseId, rollbackItem.ContentType);
					}

					return false;
				}

				return true;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<bool> DeleteSpecificDocumentVersion(Guid userId, string fileName, int version)
		{
			try
			{
				_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

				using var tx = StateManager.CreateTransaction();
				var result = await _documents.TryGetValueAsync(tx, userId.ToString());

				if (result.HasValue && result.Value.TryGetValue(fileName, out var metadataList) && metadataList is { Count: > 0 })
				{
					var metadata = metadataList.Find(md => md.Version.Equals(version));

					if (metadata is not null)
					{
						await _blobRepository.DeleteBlobAsync(CONRAINER_NAME, metadata.BlobPath);
						metadataList.Remove(metadata);

						await _documents.SetAsync(tx, userId.ToString(), result.Value);
						await tx.CommitAsync();

						return true;
					}
				}

				return false;

			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<List<DocumentInfo>> GetDocumentsByCourses(List<string> courseIds)
		{
			try
			{
				_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

				using var tx = StateManager.CreateTransaction();
				var allDocuments = new List<DocumentInfo>();

				var enumerable = await _documents.CreateEnumerableAsync(tx);
				using var enumerator = enumerable.GetAsyncEnumerator();

				while (await enumerator.MoveNextAsync(CancellationToken.None))
				{
					var userDocuments = enumerator.Current.Value;

					foreach (var document in userDocuments)
					{
						var filteredDocuments = document.Value
							.Where(d => courseIds.Contains(d.CourseId))
							.OrderByDescending(x => x.Version)
							.ToList();

						if (filteredDocuments.Count != 0)
						{
							var latestDocument = filteredDocuments.First();

							var parsedBlob = BlobHelper.ParseBlobName(latestDocument.BlobPath);

							allDocuments.Add(new DocumentInfo()
							{
								FileName = document.Key,
								UserId = Guid.Parse(enumerator.Current.Key),
								Extension = parsedBlob[3],
								Version = Int32.Parse(parsedBlob[2]),
								CourseId = Guid.Parse(latestDocument.CourseId),
							});
						}
					}
				}

				return allDocuments;
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
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
			string connectionString = Environment.GetEnvironmentVariable(ConfigKeys.ConnectionString) ?? "UseDevelopmentStorage=true";
			_blobRepository = new BlobRepository(connectionString);
			Guard.EnsureNotNull(_blobRepository, nameof(_blobRepository));

			await SetRateLimitSettings(2, 1);
			await LoadDocuments();
		}
		private async Task LoadDocuments()
		{
			_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string,List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

			try
			{
				using var tx = StateManager.CreateTransaction();
				var result = await _blobRepository.ListBlobsAsync(CONRAINER_NAME);
				List<string> courses = [];

				foreach (var kvp in result)
				{
					Dictionary<string, List<DocumentMetadata>> userDocuments = [];

					foreach (var blob in kvp.Value)
					{
						var parsedBlob = BlobHelper.ParseBlobName(blob.BlobPaths);

						var id = parsedBlob[0];
						var fileName = parsedBlob[1];
						var version = Int32.Parse(parsedBlob[2]);
						var extension = parsedBlob[3];

						var metadata = new DocumentMetadata()
						{
							Version = version,
							BlobPath = BlobHelper.CreateBlobName(id, fileName, version, extension),
							CourseId = blob.CourseId,
						};

						if (!userDocuments.TryGetValue(fileName, out List<DocumentMetadata>? value))
						{
							userDocuments.Add(fileName, [metadata]);
						}
						else
						{
							value.Add(metadata);
						}
					}
					var userId = kvp.Key.Split('/')[0];

					await _documents.AddAsync(tx, userId, userDocuments);
				}

				await tx.CommitAsync();
			}
			catch (Exception)
			{
				throw;
			}
		}
	}
}
