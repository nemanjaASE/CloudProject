
namespace Common.Exceptions
{
	public class DependencyNotInitializedException : InvalidOperationException
	{
		public DependencyNotInitializedException(string dependencyName)
			: base($"Dependency '{dependencyName}' is not initialized. Ensure it is properly configured.")
		{
		}
	}
}
