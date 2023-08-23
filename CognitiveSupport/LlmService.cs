using OpenAI;
using OpenAI.Interfaces;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace CognitiveSupport
{
	public class LlmService
	{
		private readonly string ApiKey;
		private readonly string Endpoint;
		private readonly object _lock = new object();
		private readonly Dictionary<string, IOpenAIService> _openAIServices;


		public LlmService(
			string apiKey,
			string azureResourceName,
			List<LlmSettings.ModelDeploymentIdMap> modelDeploymentIdMaps)
		{
			ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
			if (modelDeploymentIdMaps is null || !modelDeploymentIdMaps.Any())
				throw new ArgumentNullException(nameof(modelDeploymentIdMaps));

			_openAIServices = new Dictionary<string, IOpenAIService>();
			foreach (var map in modelDeploymentIdMaps)
			{
				OpenAiOptions options = new OpenAiOptions
				{
					ApiKey = apiKey,
					ResourceName = azureResourceName,
					ProviderType = ProviderType.Azure,
					DeploymentId = map.DeploymentId,
				};
				HttpClient httpClient = new HttpClient();
				httpClient.Timeout = TimeSpan.FromSeconds(60);
				_openAIServices[map.ModelName] = new OpenAIService(options, httpClient);
			}
		}

		public async Task<string> CreateChatCompletion(
			IList<ChatMessage> messages,
			string llmModelName)
		{
			if (!_openAIServices.ContainsKey(llmModelName))
				throw new ArgumentException($"{llmModelName} is not one of the configured models. The following are the available, configured models: {string.Join(",", _openAIServices.Keys)}", nameof(llmModelName));

			var service = _openAIServices[llmModelName];
			var response = await service.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
			{
				Messages = messages,
				Model = llmModelName,
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