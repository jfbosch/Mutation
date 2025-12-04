using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace CognitiveSupport;

public class LlmService : ILlmService
{
	private readonly Dictionary<string, ChatClient> _chatClients;

	public LlmService(
		string apiKey,
		string azureResourceName,
		List<LlmSettings.ModelDeploymentIdMap> modelDeploymentIdMaps)
	{
		if (string.IsNullOrEmpty(apiKey)) throw new ArgumentNullException(nameof(apiKey));
		if (modelDeploymentIdMaps is null || !modelDeploymentIdMaps.Any())
			throw new ArgumentNullException(nameof(modelDeploymentIdMaps));

		_chatClients = new Dictionary<string, ChatClient>();

		var endpoint = new Uri($"https://{azureResourceName}.openai.azure.com/");
		var credential = new AzureKeyCredential(apiKey);
		var azureClient = new AzureOpenAIClient(endpoint, credential);

		foreach (var map in modelDeploymentIdMaps)
		{
			_chatClients[map.ModelName] = azureClient.GetChatClient(map.DeploymentId);
		}
	}

	public async Task<string> CreateChatCompletion(
		IList<ChatMessage> messages,
		string llmModelName,
		decimal temperature = 0.7m)
	{
		if (!_chatClients.ContainsKey(llmModelName))
			throw new ArgumentException($"{llmModelName} is not one of the configured models. The following are the available, configured models: {string.Join(",", _chatClients.Keys)}", nameof(llmModelName));

		var client = _chatClients[llmModelName];

		ChatCompletionOptions options = new()
		{
			Temperature = (float)temperature
		};

		ClientResult<ChatCompletion> result = await client.CompleteChatAsync(messages, options);

		if (result.Value.Content.Count > 0)
		{
			return result.Value.Content[0].Text;
		}
		return string.Empty;
	}
}