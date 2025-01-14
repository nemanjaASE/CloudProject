using System.Fabric;
using Common.Entities;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using Common.Repositories;
using Common.Mappers;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Common.Helpers;
using AutoMapper;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.ServiceFabric.Data;

namespace UserService
{
	internal sealed class UserService : StatefulService, IUser
    {
		private readonly ITableRepository<UserEntity> _tableRepository;
		private readonly IMapper _mapper;
		private IReliableDictionary<Guid, UserReliableEntity>? _users;

		private const string tableName = "UsersEdu";
		private const string USER_DICTIONARY = "users";
		public UserService(StatefulServiceContext context)
            : base(context)
		{
			_tableRepository = new TableRepository<UserEntity>(Environment.GetEnvironmentVariable("DataConnectionString"));

			var config = new MapperConfiguration(cfg =>
			{
				cfg.AddProfile(new MappingProfile());
			});

			_mapper = config.CreateMapper();
		}
		public async Task<(List<User>, int)> GetUsersPaged(int page, int pageSize)
		{
			var users = await _tableRepository.GetPagedAsync(tableName, page, pageSize);

			int numOfUsers = await _tableRepository.GetTotalCountAsync(tableName);

			return (_mapper.Map<List<User>>(users), numOfUsers);

		}
		public async Task<List<User>> GetAllUsers()
		{
			var entities = await _tableRepository.QueryAsync(tableName);

			return _mapper.Map<List<User>>(entities);
		}
		public async Task<bool> CheckIfUserExistsByEmail(string email)
		{
			var users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);

			try
			{
				using (var tx = StateManager.CreateTransaction())
				{
					var enumerable = await users.CreateEnumerableAsync(tx);

					using (var enumerator = enumerable.GetAsyncEnumerator())
					{
						while (await enumerator.MoveNextAsync(default(CancellationToken)))
						{
							if (enumerator.Current.Value.Email.Equals(email))
							{
								return true;
							}
						}
					}
				}

				return false;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<bool> CheckIfUserExistsById(Guid id)
		{
			var users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);

			try
			{
				using (var tx = StateManager.CreateTransaction())
				{
					var enumerable = await users.CreateEnumerableAsync(tx);

					using (var enumerator = enumerable.GetAsyncEnumerator())
					{
						while (await enumerator.MoveNextAsync(default(CancellationToken)))
						{
							if (enumerator.Current.Key.Equals(id))
							{
								return true;
							}
						}
					}
				}

				return false;
			}
			catch (Exception)
			{
				throw;
			}
		}
		public async Task<bool> AddNewUser(User newUser)
		{
			UserEntity entity = _mapper.Map<UserEntity>(newUser);

			entity.Id = Guid.NewGuid();
			entity.PartitionKey = newUser.Role.ToString();
			entity.RowKey = newUser.Email;
			entity.Timestamp = DateTime.UtcNow;
			entity.Password = PasswordHasher.HashPassword(newUser.Password);

			UserReliableEntity reliableEntity = _mapper.Map<UserReliableEntity>(newUser);

			reliableEntity.Password = entity.Password;

			bool reliableDictionarySuccess = false;

			using (var tx = StateManager.CreateTransaction())
			{
				var reliableDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);

				await reliableDictionary.SetAsync(tx, entity.Id, reliableEntity);
				await tx.CommitAsync();

				reliableDictionarySuccess = true;
			}

			try
			{
				await _tableRepository.InsertOrMergeAsync(tableName, entity);
			}
			catch (Exception ex)
			{
				if (reliableDictionarySuccess)
				{
					using (var tx = StateManager.CreateTransaction())
					{
						var reliableDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);
						await reliableDictionary.TryRemoveAsync(tx, entity.Id);
						await tx.CommitAsync();
					}
				}

				throw new Exception("Azure Table write failed. Transaction rolled back.", ex);
			}

			return true;
		}
		public async Task<bool> UpdateUser(User user)
		{
			string filter = TableQuery.GenerateFilterConditionForGuid("Id", QueryComparisons.Equal, user.Id);

			var existingEntity = (await _tableRepository.QueryAsync(tableName, filter)).FirstOrDefault();

			if(existingEntity is null)
			{
				return false;
			}

			if (!existingEntity.RowKey.Equals(user.Email) && await CheckIfUserExistsByEmail(user.Email))
			{
				return false;
			}

			UserEntity entity = _mapper.Map<UserEntity>(user);

			entity.PartitionKey = user.Role.ToString();
			entity.RowKey = user.Email;
			entity.Timestamp = DateTime.UtcNow;
			entity.Password = existingEntity.Password;

			using (var tx = StateManager.CreateTransaction())
			{
				var reliableDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);

				ConditionalValue<UserReliableEntity> result = await reliableDictionary.TryGetValueAsync(tx, user.Id);

				if (result.HasValue)
				{
					result.Value.Email = user.Email;
					result.Value.Role = user.Role.ToString();
	
					await reliableDictionary.SetAsync(tx, user.Id, result.Value);

					await _tableRepository.InsertOrMergeAsync(tableName, entity);

					if (!existingEntity.RowKey.Equals(entity.RowKey) || !existingEntity.PartitionKey.Equals(entity.PartitionKey))
					{
						await _tableRepository.DeleteAsync(tableName, existingEntity.PartitionKey, existingEntity.RowKey);
					}

					await tx.CommitAsync();
				}
			}

			return true;
		}

		public async Task<bool> DeleteUser(Guid id)
		{
			var reliableDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);

			string filter = TableQuery.GenerateFilterConditionForGuid("Id", QueryComparisons.Equal, id);

			var entity = (await _tableRepository.QueryAsync(tableName, filter)).FirstOrDefault();

			if (entity is null)
			{
				return false;
			}

			using (var tx = StateManager.CreateTransaction())
			{
				await _tableRepository.DeleteAsync(tableName, entity.PartitionKey, entity.RowKey);

				var result = await reliableDictionary.TryRemoveAsync(tx, id);

				await tx.CommitAsync();
			}

			return true;
		}

		public async Task<User> GetUserByEmail(string email)
		{
			var users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);

			try
			{
				using (var tx = StateManager.CreateTransaction())
				{
					var enumerable = await users.CreateEnumerableAsync(tx);

					using (var enumerator = enumerable.GetAsyncEnumerator())
					{
						while (await enumerator.MoveNextAsync(default(CancellationToken)))
						{
							if (enumerator.Current.Value.Email.Equals(email))
							{
								User user = _mapper.Map<User>(enumerator.Current.Value);
								user.Id = enumerator.Current.Key;

								return user;
							}
						}
					}
				}

				return null;
			}
			catch (Exception)
			{
				throw;
			}
		}

		public async Task<User> GetUserById(Guid id)
		{
			string filter = TableQuery.GenerateFilterConditionForGuid("Id", QueryComparisons.Equal, id);

			var entity = (await _tableRepository.QueryAsync(tableName, filter)).FirstOrDefault();

			return _mapper.Map<User>(entity);
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
			await LoadUsers();
		}

		private async Task LoadUsers()
		{
			_users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);

			try
			{
				using (var transaction = StateManager.CreateTransaction())
				{
					var entities = await _tableRepository.QueryAsync(tableName);

					if (!entities.Any()) return;
					else
					{
						foreach (var entity in entities)
						{
							await _users.AddAsync(transaction, entity.Id, _mapper.Map<UserReliableEntity>(entity));
						}
					}

					await transaction.CommitAsync();
				}
			} catch (Exception)
			{
				throw;
			}
		}
	}
}
