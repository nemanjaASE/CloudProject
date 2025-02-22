using System.Fabric;
using AutoMapper;
using Common.Constants;
using Common.Entities;
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
using Microsoft.WindowsAzure.Storage.Table;

namespace CourseServic
{
	internal sealed class CourseServic(StatefulServiceContext context) : StatefulService(context), ICourse
    {
        private ITableRepository<CourseEntity>? _tableRepository;
		private IReliableDictionary<string, List<Course>>? _courses;
		private IMapper _mapper;

		private const string COURSE_TABLE_NAME = ConfigKeys.CourseTableName;
		private const string COURSES_DICTIONARY = ConfigKeys.ReliableCollectionCourse;

		#region DELETE
		public async Task<bool> DeleteCourse(Guid courseId, Guid professorId)
		{
			try
			{
				string filter = TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, courseId.ToString());

				var entity = (await _tableRepository.QueryAsync(COURSE_TABLE_NAME, filter)).FirstOrDefault();
				if (entity is null)
				{
					return false;
				}

				await _tableRepository.DeleteAsync(COURSE_TABLE_NAME, entity.PartitionKey, entity.RowKey);

				_courses = await StateManager.GetOrAddAsync<IReliableDictionary<string, List<Course>>>(COURSES_DICTIONARY);
				using var tx = StateManager.CreateTransaction();

				var result = await _courses.TryGetValueAsync(tx, professorId.ToString());
				if (!result.HasValue)
				{
					return false;
				}

				var updatedCourses = result.Value.Where(course => course.CourseId != courseId).ToList();

				if (updatedCourses.Count == result.Value.Count)
				{
					return false;
				}

				await _courses.SetAsync(tx, professorId.ToString(), updatedCourses);
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
		public async Task<List<Course>> GetCoursesForProfessor(Guid professorId)
		{
			try
			{
				using var tx = StateManager.CreateTransaction();
				_courses = await StateManager.GetOrAddAsync<IReliableDictionary<string, List<Course>>>(COURSES_DICTIONARY);

				var tryGetResult = await _courses.TryGetValueAsync(tx, professorId.ToString());

				if (tryGetResult.HasValue)
				{
					return tryGetResult.Value;
				}

				return [];
			}
			catch (Exception e)
			{

				throw new Exception(e.Message);
			}
		}
		public async Task<Course?> GetCourse(Guid professorId, string courseId)
		{
			try
			{
				_courses = await StateManager.GetOrAddAsync<IReliableDictionary<string, List<Course>>>(COURSES_DICTIONARY);
				using var tx = StateManager.CreateTransaction();

				var result = await _courses.TryGetValueAsync(tx, professorId.ToString());

				if (!result.HasValue)
				{
					return null;
				}

				foreach (var course in result.Value)
				{
					if (courseId.Equals(course.CourseId.ToString()))
					{
						return course;
					}
				}

				return null;
			}
			catch (Exception)
			{
				return null;
			}
		}
		public async Task<Course?> GetCourseById(string courseId)
		{
			try
			{
				using var tx = StateManager.CreateTransaction();
				_courses = await StateManager.GetOrAddAsync<IReliableDictionary<string, List<Course>>>(COURSES_DICTIONARY);

				var enumerable = await _courses.CreateEnumerableAsync(tx);
				using var enumerator = enumerable.GetAsyncEnumerator();

				while (await enumerator.MoveNextAsync(CancellationToken.None))
				{
					var res = enumerator.Current.Value.Where(c => c.CourseId.Equals(Guid.Parse(courseId))).FirstOrDefault();

					if (res is not null)
					{
						return res;
					}
				}

				return null;
			}
			catch (Exception)
			{

				throw;
			}
		}
		public async Task<List<Course>> GetAllCourses()
		{
			try
			{
				using var tx = StateManager.CreateTransaction();
				_courses = await StateManager.GetOrAddAsync<IReliableDictionary<string, List<Course>>>(COURSES_DICTIONARY);

				var allCourses = new List<Course>();

				var enumerable = await _courses.CreateEnumerableAsync(tx);
				using var enumerator = enumerable.GetAsyncEnumerator();

				while (await enumerator.MoveNextAsync(CancellationToken.None))
				{
					allCourses.AddRange(enumerator.Current.Value);
				}

				return allCourses;
			}
			catch (Exception e)
			{

				throw new Exception(e.Message);
			}
		}
		public async Task<(List<Course>, int)> GetCoursesPaged(int page, int pageSize, Guid professorId)
		{
			try
			{
				var courses = await _tableRepository.GetPagedAsync(COURSE_TABLE_NAME, page, pageSize, "PartitionKey", professorId.ToString());

				int numOfCourses = await _tableRepository.GetTotalCountAsync(COURSE_TABLE_NAME, "PartitionKey", professorId.ToString());

				return (_mapper.Map<List<Course>>(courses), numOfCourses);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		#endregion
		#region POST
		public async Task<bool> AddCourse(Course course)
		{
			try
			{
				var courses = await GetCoursesForProfessor(course.AuthorId);

				var result = courses.Where(c => c.Title.Equals(course.Title)).FirstOrDefault();

				if (result is not null)
				{
					return false;
				}

				var entity = _mapper.Map<CourseEntity>(course);
				entity.RowKey = Guid.NewGuid().ToString();

				await _tableRepository.InsertOrMergeAsync(COURSE_TABLE_NAME, entity);

				_courses = await StateManager.GetOrAddAsync<IReliableDictionary<string, List<Course>>>(COURSES_DICTIONARY);

				using var tx = StateManager.CreateTransaction();

				var existingCourses = await _courses.TryGetValueAsync(tx, course.AuthorId.ToString());
				course.CourseId = Guid.Parse(entity.RowKey);
				List<Course> updatedCourses = existingCourses.HasValue
					? new List<Course>(existingCourses.Value) { course }
					: [course];

				await _courses.SetAsync(tx, course.AuthorId.ToString(), updatedCourses);

				await tx.CommitAsync();

				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}
		#endregion
		#region PUT
		public async Task<bool> UpdateCourse(Course updatedCourse)
		{
			try
			{
				var course = await GetCourse(updatedCourse.AuthorId, updatedCourse.CourseId.ToString());

				if (course is null) { return false; }

				var courseEntity = _mapper.Map<CourseEntity>(course);

				courseEntity.Description = updatedCourse.Description;
				courseEntity.Title = updatedCourse.Title;

				_courses = await StateManager.GetOrAddAsync<IReliableDictionary<string, List<Course>>>(COURSES_DICTIONARY);

				using var tx = StateManager.CreateTransaction();

				var existingCourses = await _courses.TryGetValueAsync(tx, updatedCourse.AuthorId.ToString());

				if (!existingCourses.HasValue) { return false; }

				var result = existingCourses.Value.Where(c => c.Title.Equals(updatedCourse.Title) && !c.CourseId.Equals(updatedCourse.CourseId)).FirstOrDefault();

				if (result is not null) { return false; }

				var coursesList = new List<Course>(existingCourses.Value);
				var index = coursesList.FindIndex(c => c.CourseId.Equals(updatedCourse.CourseId));

				if (index.Equals(-1)) { return false; }

				updatedCourse.CreatedDate = coursesList[index].CreatedDate;
				coursesList[index] = updatedCourse;

				await _courses.SetAsync(tx, updatedCourse.AuthorId.ToString(), coursesList);
				await tx.CommitAsync();

				await _tableRepository.InsertOrMergeAsync(COURSE_TABLE_NAME, courseEntity);

				return true;
			}
			catch (Exception)
			{
				return false;
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
			_tableRepository = new TableRepository<CourseEntity>(Environment.GetEnvironmentVariable(ConfigKeys.ConnectionString) ?? "UseDevelopmentStorage=true");
			Guard.EnsureNotNull(_tableRepository, nameof(_tableRepository));

			var config = new MapperConfiguration(cfg =>
			{
				cfg.AddProfile(new MappingProfile());
			});

			_mapper = config.CreateMapper();

			await LoadCourses();
		}
		private async Task LoadCourses()
		{
			_courses = await StateManager.GetOrAddAsync<IReliableDictionary<string, List<Course>>>(COURSES_DICTIONARY);

			try
			{
				using var tx = StateManager.CreateTransaction();
				Dictionary<string, List<Course>> courses = [];

				var result = await _tableRepository.QueryAsync(COURSE_TABLE_NAME);

				foreach (var course in result)
				{
					var newCourse = _mapper.Map<Course>(course);

					if (!courses.TryGetValue(course.PartitionKey, out List<Course>? value))
					{
						courses.Add(course.PartitionKey, [newCourse]);
					} else
					{
						value.Add(newCourse);
					}
				}

				foreach (var course in courses)
				{
					await _courses.AddAsync(tx, course.Key, course.Value);
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
