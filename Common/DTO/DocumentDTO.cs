using Common.Enums;

namespace Common.DTO
{
	public class DocumentDTO
	{
		public Guid UserId { get; set; }
		public string FileName { get; set; }
		public string ContentType { get; set; }
		public DocumentExtension Extension { get; set; }
		public Guid CourseId { get; set; }
		public byte[] Content { get; set; }
	}
}
