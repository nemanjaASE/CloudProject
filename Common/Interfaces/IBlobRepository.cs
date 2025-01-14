using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Common.Repositories
{
	public interface IBlobRepository
	{
		Task CreateContainerIfNotExistsAsync(string containerName);
		Task UploadBlobAsync(string containerName, string blobName, byte[] content, string contentType = null);
		Task<(byte[] Content, string ContentType)> DownloadFileAsBytesAsync(string containerName, string blobName);
		Task DeleteBlobAsync(string containerName, string blobName);
		Task<Dictionary<string, List<string>>> ListBlobsAsync(string containerName);
		Task<List<string>> ListBlobsInDirectoryAsync(string containerName, string directoryPrefix);
		Task<bool> BlobExistsAsync(string containerName, string blobName);
		Task DeleteContainerAsync(string containerName);
	}
}
