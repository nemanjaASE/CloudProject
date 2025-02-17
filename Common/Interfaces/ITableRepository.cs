using Microsoft.WindowsAzure.Storage.Table;

namespace Common.Interfaces
{
	public interface ITableRepository<T> where T : class, ITableEntity
	{
		Task InsertOrMergeAsync(string tableName, T entity);
		Task<T> RetrieveAsync(string tableName, string partitionKey, string rowKey);
		Task DeleteAsync(string tableName, string partitionKey, string rowKey);
		Task<IEnumerable<T>> QueryAsync(string tableName, string filter = null);
		Task<int> GetTotalCountAsync(string tableName, string field = null, string value = null);
		Task<IEnumerable<T>> GetPagedAsync(string tableName, int page, int pageSize, string field = null, string value = null);
	}
}
