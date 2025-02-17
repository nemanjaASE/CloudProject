using GroqApiLibrary;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Common.Models;

namespace Common.Helpers
{
	public static class GroqApi
	{
		public static string apiKey = "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";

		private static readonly Dictionary<string, ModelInfo> chatModels = new Dictionary<string, ModelInfo>
	{
		{ "mixtral-8x7b-32768", new ModelInfo(){
			ContextWindow = 32768,
			OwnedBy = "Mistral AI",
			}
		},
		{ "llama-3.2-11b-vision-preview", new ModelInfo(){
			ContextWindow = 8192,
			OwnedBy = "Meta",
			}
		},
		{ "gemma2-9b-it", new ModelInfo(){
			ContextWindow = 8192,
			OwnedBy = "Google",
			}
		},
		{ "llama-3.3-70b-versatile", new ModelInfo(){
			ContextWindow = 32768,
			OwnedBy = "Meta",
			}
		},
		{ "llama-3.2-90b-vision-preview", new ModelInfo(){
			ContextWindow = 8192,
			OwnedBy = "Meta",
			}
		},
		{ "llama-3.1-8b-instant", new ModelInfo(){
			ContextWindow = 131072,
			OwnedBy = "Meta",
			}
		},
		{ "deepseek-r1-distill-llama-70b", new ModelInfo(){
			ContextWindow = 131072,
			OwnedBy = "DeepSeek / Meta",
			}
		},
		{ "llama3-8b-8192", new ModelInfo(){
			ContextWindow = 8192,
			OwnedBy = "Meta",
			}
		},
		{ "llama3-70b-8192", new ModelInfo(){
			ContextWindow = 8192,
			OwnedBy = "Meta",
			}
		}
	};
		public static async Task<Dictionary<string, ModelInfo>> GetAllModels()
		{
			return chatModels;
		}
		public static async Task<T?> GetApiResponse<T>(string userPrompt, string systemPrompt, ModelSettings settings)
		{
			var groqApi = new GroqApiClient(apiKey);

			var request = new JsonObject
			{
				["model"] = settings.ModelName,
				["max_tokens"] = settings.MaxTokens,
				["temperature"] = settings.Temperature,
				["messages"] = new JsonArray
				{
					new JsonObject
					{
						["role"] = "system",
						["content"] = systemPrompt,
					},
					new JsonObject
					{
						["role"] = "user",
						["content"] = userPrompt,
					}
				}
			};

			try
			{
				var result = await groqApi.CreateChatCompletionAsync(request);

				if (result is null)
				{
					return default;
				}

				var data = result["choices"]?[0]?["message"]?["content"];

				if (data is null)
				{
					return default;
				}

				if (typeof(T) == typeof(string))
				{
					return data.GetValue<T>();
				}
				else if (settings.ModelName.Contains("deepseek"))
				{
					string cleansedResponse = RemoveThinkTags(data.ToString());

					cleansedResponse = ExtractJson(cleansedResponse);

					string formattedJson = FormatJson(cleansedResponse);

					_ = JsonParser.JsonParse<T>(formattedJson, out T? converted);

					return converted;
				}
				else
				{
					_ = JsonParser.JsonParse<T>(data.ToString(), out T? converted);
					return converted;
				}
			}
			catch (Exception)
			{
				return default;
			}
		}
		private static int GetContextWindow(string modelName)
		{
			chatModels.TryGetValue(modelName, out ModelInfo? value);

			return value is not null ? value.ContextWindow : -1;
		}
		public static int GetMaxTokens(int promptLength, string modelName)
		{
			int contextWindowLength = GetContextWindow(modelName);

			return (int)Math.Ceiling((contextWindowLength - promptLength) / 4.0);
		}
		public static int CountTokens(string text)
		{
			return (int)Math.Ceiling(text.Length / 4.0);
		}
		private static string RemoveThinkTags(string input)
		{
			string pattern = @"<think>.*?</think>";
			return Regex.Replace(input, pattern, string.Empty, RegexOptions.Singleline);
		}

		private static string ExtractJson(string input)
		{
			Match match = Regex.Match(input, @"```json\s*(\{.*\})\s*```", RegexOptions.Singleline);
			return match.Success ? match.Groups[1].Value : input;
		}

		private static string FormatJson(string jsonString)
		{
			try
			{
				var parsedJson = JToken.Parse(jsonString);
				return parsedJson.ToString(Formatting.Indented);
			}
			catch (JsonReaderException ex)
			{
				return $"Invalid JSON: {ex.Message}";
			}
		}
	}
}
