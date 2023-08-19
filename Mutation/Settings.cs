namespace Mutation;

public class Settings
{
	public string UserInstructions { get; set; }

	public AudioSettings AudioSettings { get; set; }
	public AzureComputerVisionSettings AzureComputerVisionSettings { get; set; }
	public OpenAiSettings OpenAiSettings { get; set; }
}

public class AudioSettings
{
	public string MicrophoneToggleMuteHotKey { get; set; }
}

public class AzureComputerVisionSettings
{
	public string ScreenshotHotKey { get; set; }
	public string ScreenshotOcrHotKey { get; set; }
	public string OcrHotKey { get; set; }
	public string SubscriptionKey { get; set; }
	public string Endpoint { get; set; }
}

public class OpenAiSettings
{
	public string SpeechToTextHotKey { get; set; }
	public string ApiKey { get; set; }
	public string Endpoint { get; set; }
	public string TempDirectory { get; set; }
	public string SpeechToTextPrompt { get; set; }

}
