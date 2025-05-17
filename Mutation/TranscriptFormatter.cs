using CognitiveSupport;
using CognitiveSupport.Extensions;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace Mutation;

public class TranscriptFormatter
{
	private readonly Settings _settings;
	private readonly ILlmService _llmService;

	public TranscriptFormatter(Settings settings, ILlmService llmService)
	{
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));
		_llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
	}

	public string ApplyRules(string transcript, bool manualPunctuation)
	{
		if (transcript is null)
			return transcript;

		string text = transcript;
		if (manualPunctuation)
		{
			text = text.RemoveSubstrings(",", ".", ";", ":", "?", "!", "...", "…");
			text = text.Replace("  ", " ");
		}

		var rules = _settings.LlmSettings?.TranscriptFormatRules ?? new List<LlmSettings.TranscriptFormatRule>();
		text = text.FormatWithRules(rules);
		text = text.CleanupPunctuation();
		return text;
	}

	public async Task<string> FormatWithLlmAsync(string transcript, string systemPrompt)
	{
		if (transcript is null)
			return transcript;

		var messages = new List<ChatMessage>
		  {
				ChatMessage.FromSystem($"{systemPrompt}"),
				ChatMessage.FromUser($"Reformat the following transcript: {transcript}")
		  };

		string formattedText = await _llmService.CreateChatCompletion(messages, Models.Gpt_4);
		return formattedText.FixNewLines();
	}
}
