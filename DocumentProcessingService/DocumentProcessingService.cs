using System.Diagnostics;
using System.Fabric;
using System.Text;
using System.Text.Json;
using Common.Constants;
using Common.Entities;
using Common.Guard;
using Common.Helpers;
using Common.Interfaces;
using Common.Models;
using Common.Schemas;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace DocumentProcessingService
{
    internal sealed class DocumentProcessingService(StatefulServiceContext context) : StatefulService(context) , IDocumentProcessing
    {
		private IDocument? _documentService;
		private IAnalysis? _analysisService;
		private IProcessingTimeEstimator? _processingTimeEstimator;

		private const string DOCUMENT_QUEUE = ConfigKeys.ReliableQueue;
		private const string SETTINGS_DICTIONARY = ConfigKeys.ReliableDictionarySettings;

		private IReliableQueue<DocumentToAnalyze>? _reliableQueue;
		private IReliableDictionary<string, ModelSettings>? _settingsDictionary;
		private readonly ServiceClientFactory _factory = new();

		public async Task<bool> ProcessDocument(DocumentInfo document, Guid userId)
		{
			try
			{
				var queueItem = new DocumentToAnalyze()
				{
					FileName = document.FileName,
					Version = document.Version,
					Extension = document.Extension,
					UserId = userId,
					CourseId = document.CourseId,
				};

				_reliableQueue = await StateManager.GetOrAddAsync<IReliableQueue<DocumentToAnalyze>>(DOCUMENT_QUEUE);

				using var tx = StateManager.CreateTransaction();
				await _reliableQueue.EnqueueAsync(tx, queueItem);
				await tx.CommitAsync();

				return true;
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}
		private async Task<bool> AnalyzeDocument(DocumentToAnalyze documentInfo)
		{
			ModelSettings? modelSettings = await GetSettings();

			if(modelSettings is null) { return false; }

			string fileName = $"{documentInfo.FileName}_{documentInfo.Version}.{documentInfo.Extension}";

			var watch = Stopwatch.StartNew();

			var (documentContent, contentType) = await GetDocument(documentInfo);

			if (documentContent is null || contentType is null || documentContent.Length.Equals(0) ) { return false; }

			if (!await AddInitAnalysis(documentInfo)) { return false; }

			try
			{
				string text = FileHelper.GetTextBasedOnContent(documentContent, contentType);
				int textLength = text.Length;
				string jsonSchema = GetJsonSchema();
				StringBuilder textToAnalyze = new();
				StringBuilder systemPromptBuilder = new();
			
				systemPromptBuilder.Append("You are a helpful assistant and your job is to analyze text and give analyses in JSON.\n");
				systemPromptBuilder.Append("The JSON object must use the schema: ");
				systemPromptBuilder.Append(jsonSchema);
				systemPromptBuilder.Append(". Do not include any notes, explanations, or additional comments outside the JSON structure. ");
				systemPromptBuilder.Append("Do not include anything before the open curly brackets.");
				systemPromptBuilder.Append("Issues needs to reflect to the score. ");
				
				string systemPrompt = systemPromptBuilder.ToString();

				string additionalRequirementsString = String.Join('\n', modelSettings.AdditionalRequirements);

				int maxChunkTokens = GroqApi.GetMaxTokens((systemPrompt.Length + additionalRequirementsString.Length), modelSettings.ModelName);

				List<string> chunks = SplitText(text, maxChunkTokens);

				List<Common.Models.Improvement> allImprovements = [];
				List<Common.Models.Reference> allReferences = [];
				int totalScore = 0;
				int numScores = 0;

				foreach (var chunk in chunks)
				{
					string promptTemplate = $@"
											Additional requirements:
											{additionalRequirementsString}

											Here is the text (part 1/{chunks.Count}):
											{chunk}";

					var response = await GroqApi.GetApiResponse<AnalysisResult>(promptTemplate, systemPrompt, modelSettings);

					if (response != null)
					{
						allImprovements.AddRange(response.PotentialImprovements);
						allReferences.AddRange(response.References);
						totalScore += response.Score;
						numScores++;
					}
				}
				watch.Stop();

				var elapsedTime = watch.ElapsedMilliseconds;
				var elapsedTimeSecond = Math.Round((elapsedTime / 1000.0), 2);

				int finalScore = numScores > 0 ? totalScore / numScores : 0;

				await UpdateToAnalayzed(finalScore, allImprovements, allReferences, elapsedTimeSecond, documentInfo.UserId, fileName);

				await _processingTimeEstimator.LogProcessingTime(textLength, elapsedTime);

				return true;
			}
			catch (Exception)
			{
				await UpdateToNotAnalyzed(documentInfo.UserId, fileName);
				return false;
			}
		}
		public async Task<string> AnalyzeSuggestions(List<Common.Models.Improvement> improvements)
		{
			try
			{
				ModelSettings? modelSettings = await GetSettings();

				if (modelSettings is null)
				{
					return String.Empty;
				}

				StringBuilder textToAnalyze = new();

				foreach (var item in improvements)
				{
					textToAnalyze.AppendLine(item.Suggestion);
				}

				StringBuilder system = new();
				system.Append("You are an intelligent assistant tasked with analyzing feedback provided to students on their assignments.\n");
				system.Append("Your goal is to identify the most common, recurring mistakes that appear across multiple student submissions.\n");
				system.Append("Focus on spotting patterns and trends in the types of errors students make.\n");
				system.Append("Look for errors that repeat frequently across different students' work, and categorize them by the nature of the mistake (e.g., unclear arguments, incorrect grammar, formatting issues, logical fallacies, etc.).\n");
				system.Append("Provide a summary that highlights the most common mistakes and their frequency, specifying the areas where students tend to make the same errors repeatedly.\n");
				system.Append("Additionally, identify the most common categories of mistakes that need attention for improvement, and point out areas where students could benefit from more practice.\n");

				string systemPrompt = system.ToString();

				string promptTemplate = $"Here is the suggestions: {textToAnalyze.ToString()}";

				return await GroqApi.GetApiResponse<string>(promptTemplate, systemPrompt, modelSettings);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message);
			}
		}	

		private static string GetJsonSchema()
		{
			JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };
			JsonSerializerOptions options = jsonSerializerOptions;
			return JsonSerializer.Serialize(AnalysisSchema.AnalysisModelJsonSchema(), options);
		}
		private async Task<(byte[]?, string?)> GetDocument(DocumentToAnalyze document)
		{
			var (documentContent, contentType) = await _documentService.DownloadDocument(new DocumentInfo()
			{
				FileName = document.FileName,
				Version = document.Version,
				Extension = document.Extension,
			}, document.UserId);

			return (documentContent, contentType);
		}

		private async Task<bool> AddInitAnalysis(DocumentToAnalyze document)
		{
			var analysis = new Analysis()
			{
				UserId = document.UserId,
				FileName = $"{document.FileName}_{document.Version}.{document.Extension}",
				PotentialImprovements = [],
				References = [],
				Score = 0,
				Status = Common.Enums.AnalysisStatus.IN_PROGRESS,
				ProcessTimeS = 0,
				CourseId = document.CourseId,
			};

			return await _analysisService.AddAnalysis(analysis);
		}
		private async Task<bool> UpdateToNotAnalyzed(Guid userId, string fileName)
		{
			var analysis = new Analysis()
			{
				Status = Common.Enums.AnalysisStatus.NOT_ANALYZED,
				Score = 0,
				PotentialImprovements = [],
				References = [],
				ProcessTimeS = 0,
				UserId = userId,
				FileName = fileName,
			};

			return await _analysisService.UpdateAnalysis(analysis);
		}
		private async Task<bool> UpdateToAnalayzed(int finalScore, List<Common.Models.Improvement> improvements, List<Common.Models.Reference> references, double elapsedTimeSecond, Guid userId, string fileName)
		{
			var analysis = new Analysis()
			{
				Status = Common.Enums.AnalysisStatus.ANALYZED,
				Score = finalScore,
				PotentialImprovements = improvements,
				References = references,
				ProcessTimeS = elapsedTimeSecond,
				UserId = userId,
				FileName = fileName,
			};

			return await _analysisService.UpdateAnalysis(analysis);
		}
		public async Task<bool> UpdateSettings(ModelSettings settings)
		{
			_settingsDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, ModelSettings>>(SETTINGS_DICTIONARY);

			using var tx = this.StateManager.CreateTransaction();

			try
			{
				await _settingsDictionary.SetAsync(tx, "ModelSettings", settings);
			}
			catch (Exception)
			{
				return false;
			}

			await tx.CommitAsync();

			return true;
		}
		public async Task<ModelSettings?> GetSettings()
		{
			_settingsDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, ModelSettings>>(SETTINGS_DICTIONARY);

			try
			{
				using var tx = StateManager.CreateTransaction();

				var result = await _settingsDictionary.TryGetValueAsync(tx, "ModelSettings");

				if (result.HasValue)
				{
					return result.Value;
				}
			}
			catch (Exception)
			{
				throw;
			}

			return null;
		}
		private async Task InitSettings()
		{
			_settingsDictionary = await StateManager.GetOrAddAsync<IReliableDictionary<string, ModelSettings>>(SETTINGS_DICTIONARY);
			using var tx = this.StateManager.CreateTransaction();

			var additionalRequirements = new List<string>()
			{
				"- Ensure all suggestions are relevant, specific, and practical.",
				"- Use consistent numbering or referencing when specifying locations (e.g., 'Paragraph 3, Sentence 2').",
				"- Avoid generic feedback; focus on actionable items that improve the quality of the text.",
				"- Provide at least one relevant reference in 'potential_references'.",
				"- If any grammatical errors are found, include the correction within the 'suggestion' itself.",
			};

			ModelSettings settings = new()
			{
				ModelName = "llama-3.3-70b-versatile",
				AdditionalRequirements = additionalRequirements,
				MaxTokens = 1024,
				Temperature = 0.5,
			};

			await _settingsDictionary.AddAsync(tx, "ModelSettings", settings);
			await tx.CommitAsync();
		}

		protected override async Task RunAsync(CancellationToken cancellationToken)
        {
			_documentService = await _factory.CreateServiceProxyAsync<IDocument>(ApiRoutes.DocumentService, true);
			_analysisService = await _factory.CreateServiceProxyAsync<IAnalysis>(ApiRoutes.AnalysisService, true);
			_processingTimeEstimator = await _factory.CreateServiceProxyAsync<IProcessingTimeEstimator>(ApiRoutes.ProcessingTimeEstimatorService, true);

			Guard.EnsureNotNull(_documentService, nameof(_documentService));
			Guard.EnsureNotNull(_analysisService, nameof(_analysisService));
			Guard.EnsureNotNull(_processingTimeEstimator, nameof(_processingTimeEstimator));

			await InitSettings();

			var queue = await StateManager.GetOrAddAsync<IReliableQueue<DocumentToAnalyze>>(DOCUMENT_QUEUE);
			while (!cancellationToken.IsCancellationRequested)
			{
				using var tx = this.StateManager.CreateTransaction();
				var item = await queue.TryDequeueAsync(tx);

				if (item.HasValue)
				{
					try
					{
						var result = await AnalyzeDocument(item.Value);
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Greska prilikom obrade: {ex.Message}");
					}
					finally
					{
						await tx.CommitAsync();
					}
				}
				else
				{
					Debug.WriteLine("Queue is empty.");
					await Task.Delay(3000, cancellationToken);
				}
			}
		}
		private static List<string> SplitText(string text, int maxChunkTokens)
		{
			List<string> chunks = [];
			string[] words = text.Split(' ');
			List<string> currentChunk = [];

			foreach (var word in words)
			{
				currentChunk.Add(word);
				if (GroqApi.CountTokens(string.Join(" ", currentChunk)) > maxChunkTokens)
				{
					chunks.Add(string.Join(" ", currentChunk.GetRange(0, currentChunk.Count - 1)));
					currentChunk = [word];
				}
			}

			if (currentChunk.Count > 0)
				chunks.Add(string.Join(" ", currentChunk));

			return chunks;
		}
		protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
		{
			return
			[
				new ServiceReplicaListener(serviceContext =>
					new FabricTransportServiceRemotingListener(
						serviceContext,
						this, new FabricTransportRemotingListenerSettings
							{
								ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.Default,
							}
						)
					)
			];
		}
	}
}
