using OpenAI;
using OpenAI.Interfaces;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;

namespace CognitiveSupport;

public class LlmService : ILlmService
{
	private readonly string ApiKey;
	// private readonly string Endpoint; // Endpoint is not used
	// private readonly object _lock = new object(); // _lock is not used
	private readonly Dictionary<string, IOpenAIService> _openAIServices;

	private static readonly HttpClient SharedHttpClient;

	static LlmService()
	{
		SharedHttpClient = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(60)
		};
	}

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
			// Use the shared HttpClient instance
			_openAIServices[map.ModelName] = new OpenAIService(options, SharedHttpClient);
		}
	}

	public async Task<string> CreateChatCompletion(
		IList<ChatMessage> messages,
		string llmModelName,
		decimal temperature = 0.7m)
	{
		if (!_openAIServices.ContainsKey(llmModelName))
			throw new ArgumentException($"{llmModelName} is not one of the configured models. The following are the available, configured models: {string.Join(",", _openAIServices.Keys)}", nameof(llmModelName));

		var service = _openAIServices[llmModelName];
		var response = await service.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
		{
			Messages = messages,
			Model = llmModelName,
			Temperature = (float)temperature,
		});
		if (response.Successful)
		{
			// Ensure there is content to return, though rare for this to be null on success.
			var content = response.Choices?.FirstOrDefault()?.Message?.Content;
			if (content == null)
			{
				// This case should ideally not happen if Successful is true and Choices are present.
				// However, to prevent NullReferenceException and signal an unexpected state:
				throw new LlmServiceException("LLM request reported success, but no content was found.");
			}
			return content;
		}
		else
		{
			// Use the custom LlmServiceException
			if (response.Error != null)
			{
				throw new LlmServiceException(response.Error.Code, response.Error.Message);
			}
			else
			{
				// Fallback if Error object itself is null, though the API usually provides it.
				throw new LlmServiceException("LLM request failed with an unknown error and no error details provided.");
			}
		}
	}
}