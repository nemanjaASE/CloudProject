using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
	public interface ICourse : IService
	{
		Task<bool> AddCourse(Course course);
		Task<bool> DeleteCourse(Guid courseId, Guid professorId);
		Task<List<Course>> GetAllCourses();
		Task<Course?> GetCourse(Guid professorId, string courseId);
		Task<Course?> GetCourseById(string courseId);
		Task<List<Course>> GetCoursesForProfessor(Guid professorId);
		Task<(List<Course>, int)> GetCoursesPaged(int page, int pageSize, Guid professorId);
		Task<bool> UpdateCourse(Course updatedCourse);
	}
}
