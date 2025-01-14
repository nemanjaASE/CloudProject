using Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTO
{
	public class DocumentDTO
	{
		public Guid UserId { get; set; }
		public string FileName { get; set; }
		public string ContentType { get; set; }
		public DocumentExtension Extension { get; set; }
		public byte[] Content { get; set; }
	}
}
