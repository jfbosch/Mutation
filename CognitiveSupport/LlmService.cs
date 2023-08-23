using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using OpenAI.Interfaces;
using OpenAI.Managers;
using OpenAI;
using static OpenAI.ObjectModels.Models;

namespace CognitiveSupport
{
	public class LlmService
	{
		private readonly string ApiKey;
		private readonly string Endpoint;
		private readonly object _lock = new object();
		private readonly IOpenAIService _openAIService;


		public LlmService(
			string apiKey,
			string endpoint)
		{
			ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
			Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

			OpenAiOptions options = new OpenAiOptions
			{
				ApiKey = apiKey,

			};
			HttpClient httpClient = new HttpClient();
			httpClient.Timeout = TimeSpan.FromSeconds(60);
			_openAIService = new OpenAIService(options, httpClient);
		}

		public async Task<string> ConvertAudioToText(
			IList<ChatMessage>  messages)
		{
			var response = await _openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
			{
				Messages = messages,
				//Model = Models.Gpt_3_5_Turbo
				Model = Models.Gpt_4,
			});
			if (response.Successful)
			{
				return response.Choices.First().Message.Content;
			}
			else
			{
				if (response.Error == null)
				{
					throw new Exception("Unknown Error");
				}
				return $"Error converting speech to text: {response.Error.Code} {response.Error.Message}";
			}
		}
	}
}