using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Helpers
{
	public static class BlobHelper
	{
		public static string CreateBlobName(string userId, string fileName, int version, string extension)
		{
			return $"{userId}/{fileName}_v{version}.{extension}";
		}
		public static List<string> ParseBlobName(string blobName)
		{
			var temp = blobName.Split('/');
			var userId = temp[0];
			temp = temp[1].Split("_v");
			var fileName = temp[0];
			temp = temp[1].Split('.');
			var version = temp[0];
			var extension = temp[1];

			return new List<string> { userId, fileName, version, extension };
		}
	}
}
