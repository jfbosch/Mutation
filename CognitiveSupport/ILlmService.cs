using OpenAI.ObjectModels.RequestModels;

namespace CognitiveSupport;

public interface ILlmService
{
	Task<string> CreateChatCompletion(IList<ChatMessage> messages, string llmModelName, decimal temperature = 0.7M);
}