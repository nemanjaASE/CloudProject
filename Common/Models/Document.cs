
namespace Common.Models
{
	public class Document
	{
		public string FileName { get; set; }
		public string Extension { get; set; }
		public string ContentType { get; set; }
		public Guid UserId { get; set; }
		public int Version {  get; set; }
		public byte[] Content { get; set; }
	}
}
