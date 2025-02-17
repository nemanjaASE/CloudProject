using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Common.Models
{
	public class Improvement
	{
		[JsonPropertyName("suggestion")]
		public string Suggestion { get; set; }

		[JsonPropertyName("location")]
		public string Location { get; set; }
	}

	public class Reference
	{
		[JsonPropertyName("title")]
		public string Title { get; set; }

		[JsonPropertyName("author")]
		public string Author { get; set; }
	}
	public class AnalysisResult
	{
		[JsonPropertyName("potential_improvements")]
		public List<Improvement> PotentialImprovements { get; set; }

		[JsonPropertyName("score")]
		public int Score { get; set; }

		[JsonPropertyName("potential_references")]
		public List<Reference> References { get; set; }
	}
}
