using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Entities
{
	public class DocumentToAnalyze
	{
		public string FileName { get; set; }
		public string Extension { get; set; }
		public Guid CourseId { get; set; }
		public Guid UserId { get; set; }
		public int Version { get; set; }
	}
}
