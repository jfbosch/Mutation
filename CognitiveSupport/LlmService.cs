using OpenAI.Chat;
using System.ClientModel;

namespace CognitiveSupport;

public class LlmService : ILlmService
{
	private readonly Dictionary<string, ChatClient> _chatClients;
	private readonly string _reasoningEffort;

	public LlmService(
		string apiKey,
		List<string> models,
		string reasoningEffort)
	{
		if (string.IsNullOrEmpty(apiKey)) throw new ArgumentNullException(nameof(apiKey));
		if (models is null || !models.Any())
			throw new ArgumentNullException(nameof(models));

		_chatClients = new Dictionary<string, ChatClient>();
		_reasoningEffort = reasoningEffort;

		foreach (var model in models)
		{
			_chatClients[model] = new ChatClient(model, apiKey);
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

		/*
		if (!string.IsNullOrWhiteSpace(_reasoningEffort) && Enum.TryParse<ChatReasoningEffort>(_reasoningEffort, true, out var effort))
		{
			options.ReasoningEffort = effort;
		}
		*/

		ClientResult<ChatCompletion> result = await client.CompleteChatAsync(messages, options);

		if (result.Value.Content.Count > 0)
		{
			return result.Value.Content[0].Text;
		}
		return string.Empty;
	}
}