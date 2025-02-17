using Common.Enums;
using Common.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Common.Entities
{
	public class Improvement
	{
		public string Suggestion { get; set; }
		public string Location { get; set; }
	}
	public class Reference
	{
		public string Title { get; set; }
		public string Author { get; set; }
	}

	public class AnalysisEntity : TableEntity
	{
		public string? PotentialImprovementsJson { get; set; }
		public string? ReferencesJson { get; set; }
		public int Score { get; set; }
		public string Status { get; set; }
		public double ProcessTimeS { get; set; }
		public List<Improvement> PotentialImprovements
		{
			get => string.IsNullOrEmpty(PotentialImprovementsJson)
				? new List<Improvement>()
				: JsonSerializer.Deserialize<List<Improvement>>(PotentialImprovementsJson);
			set => PotentialImprovementsJson = JsonSerializer.Serialize(value);
		}
		public List<Reference> References
		{
			get => string.IsNullOrEmpty(ReferencesJson)
				? new List<Reference>()
				: JsonSerializer.Deserialize<List<Reference>>(ReferencesJson);
			set => ReferencesJson = JsonSerializer.Serialize(value);
		}
		public string CourseId { get; set; }
	}
}
