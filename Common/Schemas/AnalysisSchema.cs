namespace Common.Schemas
{
	public static class AnalysisSchema
	{
		public static object AnalysisModelJsonSchema()
		{
			return new
			{
				type = "object",
				properties = new
				{
					potential_improvements = new
					{
						type = "array",
						items = new
						{
							type = "object",
							properties = new
							{
								suggestion = new { type = "string" },
								location = new { type = "string" }
							},
							required = new[] { "suggestion", "location" }
						}
					},
					score = new { type = "integer", minimum = 1, maximum = 10 },
					potential_references = new
					{
						type = "array",
						items = new
						{
							type = "object",
							properties = new
							{
								title = new { type = "string" },
								author = new { type = "string" }
							},
							required = new[] { "title", "author" }
						}
					}
				},
				required = new[] { "potential_improvements", "score", "potential_references" }
			};
		}
	}
}
