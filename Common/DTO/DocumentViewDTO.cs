
namespace Common.DTO
{
	public class DocumentViewDTO
	{
		public Guid StudentId { get; set; }
		public string FileName { get; set; }
		public string Extension { get; set; }
		public int Version { get; set; }
		public string CourseName { get; set; }
		public string StudentFirstName { get; set; }
		public string StudentLastName { get; set; }
	}
}
