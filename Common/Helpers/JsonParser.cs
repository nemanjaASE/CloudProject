using Common.Models;
using System.Text.Json;

namespace Common.Helpers
{
	public static class JsonParser
	{
		public static bool JsonParse<T>(string text, out T? result)
		{
			result = default;

			try
			{
				result = JsonSerializer.Deserialize<T>(text);

				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}
