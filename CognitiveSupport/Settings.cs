namespace CognitiveSupport;

public class Settings
{
	public string UserInstructions { get; set; }

	public AudioSettings AudioSettings { get; set; }
	public AzureComputerVisionSettings AzureComputerVisionSettings { get; set; }
	public SpeetchToTextSettings SpeetchToTextSettings { get; set; }
	public LlmSettings LlmSettings { get; set; }
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
	public string ApiKey { get; set; }
	public string Endpoint { get; set; }
}

public class SpeetchToTextSettings
{
	public string SpeechToTextHotKey { get; set; }
	public string ApiKey { get; set; }
	public string TempDirectory { get; set; }
	public string SpeechToTextPrompt { get; set; }

}

public class LlmSettings
{
	public string ApiKey { get; set; }
	public string ResourceName { get; set; }
	public List<ModelDeploymentIdMap> ModelDeploymentIdMaps { get; set; }
	public string FormatTranscriptPrompt { get; set; }

	public class ModelDeploymentIdMap
	{
		public string ModelName { get; set; }
		public string DeploymentId { get; set; }
	}
}
