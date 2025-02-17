
namespace Common.Models
{
	public class Document
	{
		public Guid UserId { get; set; }
		public Guid CourseId { get; set; }
		public string FileName { get; set; }
		public string ContentType { get; set; }
		public string Extension { get; set; }
		public int Version {  get; set; }
		public string CourseName { get; set; }
		public byte[] Content { get; set; }
	}
}
