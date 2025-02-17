using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Common.Interfaces;
using Common.Entities;
using System.Collections.Generic;

namespace Common.Repositories
{
	public class TableRepository<T> : ITableRepository<T> where T : class, ITableEntity, new()
	{
		private readonly CloudTableClient _tableClient;

		public TableRepository(string storageConnectionString)
		{
			var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
			_tableClient = storageAccount.CreateCloudTableClient();
		}

		private CloudTable GetTableReference(string tableName)
		{
			var table = _tableClient.GetTableReference(tableName);
			return table;
		}
		public async Task<IEnumerable<T>> GetPagedAsync(string tableName, int page, int pageSize,string field = null, string value = null)
		{
			var table = GetTableReference(tableName);

			var query = new TableQuery<T>().Take(pageSize);

			if (!string.IsNullOrEmpty(field) && !string.IsNullOrEmpty(value))
			{
				string filter = TableQuery.GenerateFilterCondition(field, QueryComparisons.Equal, value);
				query = query.Where(filter);
			}

			TableContinuationToken continuationToken = null;
			var users = new List<T>();
			int currentPage = 1;

			do
			{
				var segment = await table.ExecuteQuerySegmentedAsync(query, continuationToken);
				continuationToken = segment.ContinuationToken;

				if (currentPage == page)
				{
					users.AddRange(segment.Results);
					break;
				}

				currentPage++;
			} while (continuationToken != null && currentPage <= page);

			return users;
		}

		public async Task<int> GetTotalCountAsync(string tableName, string field = null, string value = null)
		{
			var table = GetTableReference(tableName);

			var query = new TableQuery<UserEntity>().Select(new[] { "PartitionKey" });
			TableContinuationToken continuationToken = null;

			if (!string.IsNullOrEmpty(field) && !string.IsNullOrEmpty(value))
			{
				string filter = TableQuery.GenerateFilterCondition(field, QueryComparisons.Equal, value);
				query = query.Where(filter);
			}

			int count = 0;
			do
			{
				var segment = await table.ExecuteQuerySegmentedAsync(query, continuationToken);
				continuationToken = segment.ContinuationToken;
				count += segment.Results.Count;
			} while (continuationToken != null);

			return count;
		}
		public async Task InsertOrMergeAsync(string tableName, T entity)
		{
			var table = GetTableReference(tableName);
			await table.CreateIfNotExistsAsync();

			var insertOrMergeOperation = TableOperation.InsertOrMerge(entity);
			await table.ExecuteAsync(insertOrMergeOperation);
		}

		public async Task<T> RetrieveAsync(string tableName, string partitionKey, string rowKey)
		{
			var table = GetTableReference(tableName);

			var retrieveOperation = TableOperation.Retrieve<T>(partitionKey, rowKey);
			var result = await table.ExecuteAsync(retrieveOperation);

			return result.Result as T;
		}

		public async Task DeleteAsync(string tableName, string partitionKey, string rowKey)
		{
			var table = GetTableReference(tableName);

			var entity = await RetrieveAsync(tableName, partitionKey, rowKey);
			if (entity == null)
			{
				throw new InvalidOperationException("Entity not found.");
			}

			var deleteOperation = TableOperation.Delete(entity);
			await table.ExecuteAsync(deleteOperation);
		}

		public async Task<IEnumerable<T>> QueryAsync(string tableName, string filter = null)
		{
			var table = GetTableReference(tableName);

			var query = new TableQuery<T>();
			if (!string.IsNullOrEmpty(filter))
			{
				query = query.Where(filter);
			}

			var entities = new List<T>();
			TableContinuationToken token = null;
			do
			{
				var segment = await table.ExecuteQuerySegmentedAsync(query, token);
				token = segment.ContinuationToken;
				entities.AddRange(segment.Results);
			} while (token != null);

			return entities;
		}
	}
}
