using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Common.DTO;
using Common.Entities;
using Common.Interfaces;
using Common.Models;
using Common.Repositories;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace DocumentService
{
	internal sealed class DocumentService : StatefulService, IDocument
    {
		private readonly IBlobRepository _blobRepository;
		private const string containerName = "documents";
		private IReliableDictionary<string, Dictionary<string,List<DocumentMetadata>>>? _documents;

		private const string DOCUMENT_DICTIONARY = "documents";
		public DocumentService(StatefulServiceContext context)
            : base(context)
        {
			_blobRepository = new BlobRepository(Environment.GetEnvironmentVariable("DataConnectionString"));
		}

		public async Task<(byte[] Content, string ContentType)> DownloadDocument(DocumentInfo document, Guid userId)
		{
			var blobName = CreateBlobName(userId.ToString(), document.FileName, document.Version, document.Extension);

			return await _blobRepository.DownloadFileAsBytesAsync(containerName, blobName);
		}
		public async Task<List<DocumentInfo>> GetDocumentsByUserId(Guid userId)
		{
			_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string,List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

			using (var tx = StateManager.CreateTransaction())
			{
				var result = await _documents.TryGetValueAsync(tx, userId.ToString());
				if (result.HasValue)
				{
					List<DocumentInfo> documentInfos = new List<DocumentInfo>();

					foreach (var document in result.Value)
					{
						var latestDocument = document.Value.OrderByDescending(x => x.Version).FirstOrDefault();

						var parsedBlob = ParseBlobName(latestDocument.BlobPath);

						documentInfos.Add(new DocumentInfo()
						{
							FileName = document.Key,
							Extension = parsedBlob[3],
							Version = Int32.Parse(parsedBlob[2]),
						});
					}

					return documentInfos;
				}
				else
				{
					return null;
				}
			}
		}
		public async Task<int> FindLatestVersion(Guid userId, string fileName)
		{
			_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

			int latestVersion = 0;

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

							break;
						}
					}
				}
			}

			return latestVersion;
		}
		public async Task<bool> UploadNewVersionAsync(Document document)
		{
			string blobName = CreateBlobName(document.UserId.ToString(), document.FileName, document.Version, document.Extension);

			_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

			bool reliableDictionarySuccess = true;

			using (var tx = StateManager.CreateTransaction())
			{
				var documentMetadata = new DocumentMetadata()
				{
					Version = document.Version,
					BlobPath = blobName,
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
				await _blobRepository.UploadBlobAsync(containerName, blobName, document.Content, document.ContentType);
			}

			return true;
		}
		public async Task<bool> UploadDocumentAsync(Document document)
		{
			string blobName = CreateBlobName(document.UserId.ToString(), document.FileName, document.Version, document.Extension);

			_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);
			bool reliableDictionarySuccess = true;

			using (var tx = StateManager.CreateTransaction())
			{
				var documentMetadata = new DocumentMetadata(){
					Version = document.Version,
					BlobPath = blobName,
				};

				var result = await _documents.TryGetValueAsync(tx, document.UserId.ToString());

				if (result.HasValue)
				{
					result.Value.Add(document.FileName, new List<DocumentMetadata>() { documentMetadata });
				} else
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
				await _blobRepository.UploadBlobAsync(containerName, blobName, document.Content, document.ContentType);
			}

			return true;
		}
		public async Task<bool> DeleteDocument(Guid userId, string fileName)
		{
			_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

			using (var tx = StateManager.CreateTransaction())
			{
				var result = await _documents.TryGetValueAsync(tx, userId.ToString());

				if (result.HasValue && result.Value.TryGetValue(fileName, out var metadataList) && metadataList is { Count: > 0 })
				{
					var rollbackData = new List<DocumentRollback>();

					try
					{
						foreach (var metadata in metadataList)
						{
							var (content, contentType) = await _blobRepository.DownloadFileAsBytesAsync(containerName, metadata.BlobPath);
							await _blobRepository.DeleteBlobAsync(containerName, metadata.BlobPath);
							rollbackData.Add(new DocumentRollback { BlobPath = metadata.BlobPath, Content = content, ContentType = contentType });
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
							await _blobRepository.UploadBlobAsync(containerName, rollbackItem.BlobPath, rollbackItem.Content, rollbackItem.ContentType);
						}

						return false;
					}
				}
			}

			return false;
		}
		public async Task<bool> DeleteSpecificDocumentVersion(Guid userId, string fileName, int version)
		{
			_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string, List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);

			using (var tx = StateManager.CreateTransaction())
			{
				var result = await _documents.TryGetValueAsync(tx, userId.ToString());

				if (result.HasValue && result.Value.TryGetValue(fileName, out var metadataList) && metadataList is { Count: > 0 })
				{
					var metadata = metadataList.Find(md => md.Version.Equals(version));

					if (metadata is not null)
					{
						await _blobRepository.DeleteBlobAsync(containerName, metadata.BlobPath);
						metadataList.Remove(metadata);

						await _documents.SetAsync(tx, userId.ToString(), result.Value);
						await tx.CommitAsync();

						return true;
					}
				}
			}

			return false;
		}

		private static string CreateBlobName(string userId, string fileName, int version, string extension)
		{
			return $"{userId}/{fileName}_v{version}.{extension}";
		}
		private static List<string> ParseBlobName(string blobName)
		{
			var temp = blobName.Split('/');
			var userId = temp[0];
			temp = temp[1].Split("_v");
			var fileName = temp[0];
			temp = temp[1].Split('.');
			var version = temp[0];
			var extension = temp[1];

			return new List<string> { userId, fileName, version, extension };
		}

		protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
		{
			return new List<ServiceReplicaListener>
			{
				new ServiceReplicaListener(serviceContext =>
					new FabricTransportServiceRemotingListener(
						serviceContext,
						this, new FabricTransportRemotingListenerSettings
							{
								ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.Default,
							})
					)
			};
		}
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
			await LoadDocuments();
        }
		private async Task LoadDocuments()
		{
			_documents = await StateManager.GetOrAddAsync<IReliableDictionary<string, Dictionary<string,List<DocumentMetadata>>>>(DOCUMENT_DICTIONARY);
		
			try
			{
				using (var tx = StateManager.CreateTransaction())
				{
					var result = await _blobRepository.ListBlobsAsync(containerName);

					foreach(var kvp in result)
					{
						Dictionary<string, List<DocumentMetadata>> userDocuments = [];

						foreach (var blob in kvp.Value)
						{
							var parsedBlob = ParseBlobName(blob);

							var id = parsedBlob[0];
							var fileName = parsedBlob[1];
							var version = Int32.Parse(parsedBlob[2]);
							var extension = parsedBlob[3];

							var metadata = new DocumentMetadata()
							{
								Version = version,
								BlobPath = CreateBlobName(id, fileName, version, extension),
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
			}
			catch (Exception)
			{
				throw;
			}
		}
	}
}
