using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using Common.Models;

namespace Common.Repositories
{
	public class BlobRepository : IBlobRepository
	{
		private readonly CloudBlobClient _blobClient;

		public BlobRepository(string storageConnectionString)
		{
			var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
			_blobClient = storageAccount.CreateCloudBlobClient();
		}

		private CloudBlobContainer GetContainerReference(string containerName)
		{
			var container = _blobClient.GetContainerReference(containerName);
			return container;
		}

		public async Task CreateContainerIfNotExistsAsync(string containerName)
		{
			var container = GetContainerReference(containerName);
			await container.CreateIfNotExistsAsync();
		}

		public async Task UploadBlobAsync(string containerName, string blobName, byte[] content, string courseId, string contentType = null)
		{
			var container = GetContainerReference(containerName);
			await CreateContainerIfNotExistsAsync(containerName);

			var blob = container.GetBlockBlobReference(blobName);
			if (contentType != null)
			{
				blob.Properties.ContentType = contentType;
			}

			blob.Metadata["CourseId"] = courseId;

			using (var stream = new MemoryStream(content))
			{
				await blob.UploadFromStreamAsync(stream);
			}

			await blob.SetMetadataAsync();
		}
		public async Task<(byte[] Content, string ContentType)> DownloadFileAsBytesAsync(string containerName, string blobName)
		{
			var container = GetContainerReference(containerName);
			var blobClient = container.GetBlockBlobReference(blobName);

			if (await blobClient.ExistsAsync())
			{
				await blobClient.FetchAttributesAsync();
				var contentType = blobClient.Properties.ContentType;

				using (var memoryStream = new MemoryStream())
				{
					await blobClient.DownloadToStreamAsync(memoryStream);
					return (memoryStream.ToArray(), contentType);
				}
			}
			else
			{
				throw new FileNotFoundException("File doesn't exist.");
			}
		}

		public async Task DeleteBlobAsync(string containerName, string blobName)
		{
			var container = GetContainerReference(containerName);
			var blob = container.GetBlockBlobReference(blobName);

			if (await blob.ExistsAsync())
			{
				try
				{
					await blob.DeleteAsync();
				}
				catch (Exception)
				{
					throw;
				}
			}
			else
			{
				throw new InvalidOperationException($"Blob '{blobName}' does not exist in container '{containerName}'.");
			}
		}

		public async Task<Dictionary<string, List<DocumentRead>>> ListBlobsAsync(string containerName)
		{
			var container = GetContainerReference(containerName);

			if (!await container.ExistsAsync())
			{
				throw new Exception($"Container '{containerName}' doesn't exists.");
			}

			var blobNames = new Dictionary<string, List<DocumentRead>>();
			BlobContinuationToken continuationToken = null;

			do
			{
				BlobResultSegment resultSegment = await container.ListBlobsSegmentedAsync(null, continuationToken);

				foreach (IListBlobItem blobItem in resultSegment.Results)
				{
					if (blobItem is CloudBlobDirectory directory)
					{
						var blobs = ListBlobsInDirectoryAsync(containerName, directory.Prefix);

						blobNames.Add(directory.Prefix, blobs.Result);
					}
				}

				continuationToken = resultSegment.ContinuationToken;
			} while (continuationToken != null);

			return blobNames;
		}

		public async Task<List<DocumentRead>> ListBlobsInDirectoryAsync(string containerName, string directoryPrefix)
		{
			CloudBlobContainer container = GetContainerReference(containerName);

			if (!await container.ExistsAsync())
			{
				throw new Exception($"Container '{containerName}' doesn't exist.");
			}

			var blobNames = new List<DocumentRead>();
			BlobContinuationToken continuationToken = null;

			CloudBlobDirectory directory = container.GetDirectoryReference(directoryPrefix);

			do
			{
				BlobResultSegment resultSegment = await directory.ListBlobsSegmentedAsync(continuationToken);

				foreach (IListBlobItem blobItem in resultSegment.Results)
				{
					if (blobItem is CloudBlockBlob blockBlob)
					{
						await blockBlob.FetchAttributesAsync();

						blockBlob.Metadata.TryGetValue("CourseId", out string courseId);

						blobNames.Add(new DocumentRead()
						{
							BlobPaths = blockBlob.Name,
							CourseId = courseId ?? null
						});
					}
				}

				continuationToken = resultSegment.ContinuationToken;
			} while (continuationToken != null);

			return blobNames;
		}

		public async Task<bool> BlobExistsAsync(string containerName, string blobName)
		{
			var container = GetContainerReference(containerName);
			var blob = container.GetBlockBlobReference(blobName);

			return await blob.ExistsAsync();
		}

		public async Task DeleteContainerAsync(string containerName)
		{
			var container = GetContainerReference(containerName);

			if (await container.ExistsAsync())
			{
				await container.DeleteAsync();
			}
			else
			{
				throw new InvalidOperationException($"Container '{containerName}' does not exist.");
			}
		}
	}
}
