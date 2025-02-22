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
using AutoMapper;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.ServiceFabric.Data;
using Common.Constants;
using Common.Guard;
using Common.Helpers;

namespace UserService
{
	internal sealed class UserService(StatefulServiceContext context) : StatefulService(context), IUser
    {
		private  ITableRepository<UserEntity>? _tableRepository;
		private  IMapper? _mapper;
		private IReliableDictionary<Guid, UserReliableEntity>? _users;

		private const string tableName = ConfigKeys.UsersTableName;
		private const string USER_DICTIONARY = ConfigKeys.ReliableDicitonaryUser;

		#region GET
		public async Task<(List<User>, int)> GetUsersPaged(int page, int pageSize)
		{
			try
			{
				var users = await _tableRepository.GetPagedAsync(tableName, page, pageSize);

				int numOfUsers = await _tableRepository.GetTotalCountAsync(tableName);

				return (_mapper.Map<List<User>>(users), numOfUsers);
			}
			catch (Exception e) 
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<(List<User>, int)> GetStudentsPaged(int page, int pageSize)
		{
			try
			{
				var users = await _tableRepository.GetPagedAsync(tableName, page, pageSize, "Role", UserRole.Student.ToString());

				int numOfUsers = await _tableRepository.GetTotalCountAsync(tableName, "Role", UserRole.Student.ToString());

				return (_mapper.Map<List<User>>(users), numOfUsers);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<List<User>> GetAllUsers()
		{
			try
			{
				var entities = await _tableRepository.QueryAsync(tableName);

				return _mapper.Map<List<User>>(entities);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		public async Task<User> GetUserByEmail(string email)
		{
			try
			{
				_users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);
				using var tx = StateManager.CreateTransaction();
				var enumerable = await _users.CreateEnumerableAsync(tx);

				using var enumerator = enumerable.GetAsyncEnumerator();
				while (await enumerator.MoveNextAsync(default(CancellationToken)))
				{
					if (enumerator.Current.Value.Email.Equals(email))
					{
						User user = _mapper.Map<User>(enumerator.Current.Value);
						user.Id = enumerator.Current.Key;

						return user;
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
			try
			{
				string filter = TableQuery.GenerateFilterConditionForGuid("Id", QueryComparisons.Equal, id);

				var entity = (await _tableRepository.QueryAsync(tableName, filter)).FirstOrDefault();

				return _mapper.Map<User>(entity);
			}
			catch (Exception e)
			{

				throw new Exception(e.Message);
			}
		}
		public async Task<bool> CheckIfUserExistsByEmail(string email)
		{
			try
			{
				_users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);

				using var tx = StateManager.CreateTransaction();
				var enumerable = await _users.CreateEnumerableAsync(tx);

				using var enumerator = enumerable.GetAsyncEnumerator();
				while (await enumerator.MoveNextAsync(default(CancellationToken)))
				{
					if (enumerator.Current.Value.Email.Equals(email))
					{
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
		public async Task<bool> CheckIfUserExistsById(Guid id)
		{
			try
			{
				_users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);
				using var tx = StateManager.CreateTransaction();
				var enumerable = await _users.CreateEnumerableAsync(tx);

				using var enumerator = enumerable.GetAsyncEnumerator();
				while (await enumerator.MoveNextAsync(default(CancellationToken)))
				{
					if (enumerator.Current.Key.Equals(id))
					{
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
		public async Task<(string, string)> GetUserName(Guid id)
		{
			try
			{
				string filter = TableQuery.GenerateFilterConditionForGuid("Id", QueryComparisons.Equal, id);

				var entity = (await _tableRepository.QueryAsync(tableName, filter)).FirstOrDefault();

				return (entity.FirstName, entity.LastName);
			}
			catch (Exception e)
			{

				throw new Exception(e.Message);
			}
		}
		#endregion
		#region POST
		public async Task<bool> AddNewUser(User newUser)
		{
			UserEntity entity = _mapper.Map<UserEntity>(newUser);

			UserReliableEntity reliableEntity = _mapper.Map<UserReliableEntity>(entity);

			bool reliableDictionarySuccess = false;

			try
			{
				await _tableRepository.InsertOrMergeAsync(tableName, entity);

				using var tx = StateManager.CreateTransaction();
				_users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);

				await _users.SetAsync(tx, entity.Id, reliableEntity);
				await tx.CommitAsync();

				reliableDictionarySuccess = true;
			}
			catch (Exception)
			{
				throw;
			}

			return reliableDictionarySuccess;
		}
		#endregion
		#region PUT
		public async Task<bool> UpdateUser(User user)
		{
			try
			{
				string filter = TableQuery.GenerateFilterConditionForGuid("Id", QueryComparisons.Equal, user.Id);

				var existingEntity = (await _tableRepository.QueryAsync(tableName, filter)).FirstOrDefault();

				if (existingEntity is null)
				{
					return false;
				}

				if (!existingEntity.RowKey.Equals(user.Email) && await CheckIfUserExistsByEmail(user.Email))
				{
					return false;
				}

				var entity = new UserEntity()
				{
					FirstName = user.FirstName,
					LastName = user.LastName,
					PartitionKey = user.Role.ToString(),
					RowKey = user.Email,
					Password = user.Password,
					Id = user.Id
				};

				using var tx = StateManager.CreateTransaction();
				_users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);

				ConditionalValue<UserReliableEntity> result = await _users.TryGetValueAsync(tx, user.Id);

				if (result.HasValue)
				{
					result.Value.Email = user.Email;
					result.Value.Role = user.Role.ToString();

					if (!user.Password.Equals(existingEntity.Password))
					{
						result.Value.Password = user.Password;
					}

					await _users.SetAsync(tx, user.Id, result.Value);

					await _tableRepository.InsertOrMergeAsync(tableName, entity);

					if (!existingEntity.RowKey.Equals(entity.RowKey) || !existingEntity.PartitionKey.Equals(entity.PartitionKey))
					{
						await _tableRepository.DeleteAsync(tableName, existingEntity.PartitionKey, existingEntity.RowKey);
					}

					await tx.CommitAsync();
				}

				return true;
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		#endregion
		#region DELETE
		public async Task<bool> DeleteUser(Guid id)
		{
			try
			{
				_users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);

				string filter = TableQuery.GenerateFilterConditionForGuid("Id", QueryComparisons.Equal, id);

				var entity = (await _tableRepository.QueryAsync(tableName, filter)).FirstOrDefault();

				if (entity is null)
				{
					return false;
				}

				using var tx = StateManager.CreateTransaction();
				await _tableRepository.DeleteAsync(tableName, entity.PartitionKey, entity.RowKey);

				var result = await _users.TryRemoveAsync(tx, id);

				await tx.CommitAsync();

				return true;
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
			_tableRepository = new TableRepository<UserEntity>(Environment.GetEnvironmentVariable(ConfigKeys.ConnectionString) ?? "UseDevelopmentStorage=true");

			Guard.EnsureNotNull(_tableRepository, nameof(_tableRepository));

			var config = new MapperConfiguration(cfg =>
			{
				cfg.AddProfile(new MappingProfile());
			});

			_mapper = config.CreateMapper();

			Guard.EnsureNotNull(_mapper, nameof(_mapper));

			await LoadUsers();
		}
		private async Task LoadUsers()
		{
			_users = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, UserReliableEntity>>(USER_DICTIONARY);

			try
			{
				using var transaction = StateManager.CreateTransaction();
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
			} catch (Exception)
			{
				throw;
			}
		}
	}
}
