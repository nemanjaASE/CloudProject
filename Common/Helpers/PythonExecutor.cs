
using System.Diagnostics;

namespace Common.Helpers
{
	public static class PythonExecutor
	{
		public static bool ExecuteScript(string venvPath, string filePath, string textArgument, out string output)
		{
			output = String.Empty;

			try
			{
				ProcessStartInfo start = new ProcessStartInfo
				{
					FileName = venvPath,
					Arguments = $"\"{filePath}\" \"{textArgument}\"",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				};

				using (Process process = Process.Start(start))
				{
					if (process == null)
						return false;

					output = process.StandardOutput.ReadToEnd();
					string errors = process.StandardError.ReadToEnd();
					process.WaitForExit();

					if (process.ExitCode != 0)
						return false;
				}
			}
			catch (Exception)
			{
				return false;
			}

			return true;
		}
	}
}
