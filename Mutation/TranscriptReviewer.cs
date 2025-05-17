using CognitiveSupport;
using CognitiveSupport.Extensions;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace Mutation;

public class TranscriptReviewer
{
    private readonly ILlmService _llmService;

    public TranscriptReviewer(ILlmService llmService)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
    }

    public async Task<string> ReviewAsync(string transcript, string systemPrompt, decimal temperature)
    {
        if (transcript is null)
            return transcript;

        var messages = new List<ChatMessage>
        {
            ChatMessage.FromSystem($"{systemPrompt}"),
            ChatMessage.FromUser($"Review the following transcript:{Environment.NewLine}{Environment.NewLine}{transcript}")
        };

        string review = await _llmService.CreateChatCompletion(messages, Models.Gpt_4, temperature);
        return review.FixNewLines();
    }

    public async Task<string> ApplyCorrectionsAsync(string transcript, string systemPrompt, IEnumerable<string> instructions)
    {
        if (transcript is null)
            return transcript;

        string combinedInstructions = string.Join(Environment.NewLine, instructions.Select(i => $"- {i}"));
        var messages = new List<ChatMessage>
        {
            ChatMessage.FromSystem($"{systemPrompt}"),
            ChatMessage.FromUser($"Apply the corrections and respond only with the corrected transcript.{Environment.NewLine}{Environment.NewLine}Correction Instructions:{Environment.NewLine}{combinedInstructions}{Environment.NewLine}{Environment.NewLine}Transcript:{Environment.NewLine}{transcript}")
        };

        string revision = await _llmService.CreateChatCompletion(messages, Models.Gpt_4);
        return revision.FixNewLines();
    }
}
