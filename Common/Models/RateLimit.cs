
namespace Common.Models
{
	public class RateLimit
	{
		public uint MaxAttempts { get; set; }
		public uint TimeInterval { get; set; }
	}
}
