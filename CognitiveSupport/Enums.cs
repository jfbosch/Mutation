using System.Runtime.Serialization;

namespace CognitiveSupport;

public enum OcrReadingOrder
{
	/// <summary>
	/// Strict left-to-right, top-to-bottom sequencing (the “basic” API option).
	/// </summary>
	[EnumMember(Value = "basic")]
	LeftToRightTopToBottom = 0,

	/// <summary>
	/// Groups lines and preserves natural reading order across columns and sections (the “natural” API option).
	/// </summary>
	[EnumMember(Value = "natural")]
	TopToBottomColumnAware = 1
}

public enum SpeechToTextProviders
{
	None = 0,
	OpenAi = 1,
	Deepgram = 2,
}