using Common.Exceptions;

namespace Common.Guard
{
	public static class Guard
	{
		public static void EnsureNotNull<T>(T obj, string name)
		{
			if (obj == null)
			{
				throw new DependencyNotInitializedException(name);
			}
		}
	}
}
