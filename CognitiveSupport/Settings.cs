using System.Drawing;

namespace CognitiveSupport;

public class Settings
{
	public string UserInstructions { get; set; }

	public AudioSettings AudioSettings { get; set; }
	public AzureComputerVisionSettings AzureComputerVisionSettings { get; set; }
	public SpeetchToTextSettings SpeetchToTextSettings { get; set; }
	public LlmSettings LlmSettings { get; set; }
	public TextToSpeechSettings TextToSpeechSettings { get; set; }

	public MainWindowUiSettings MainWindowUiSettings { get; set; } = new MainWindowUiSettings();

	public HotKeyRouterSettings HotKeyRouterSettings { get; set; } = new HotKeyRouterSettings();

	public Settings()
	{
	}

	public Settings(string userInstructions, AudioSettings audioSettings, AzureComputerVisionSettings azureComputerVisionSettings, SpeetchToTextSettings speetchToTextSettings, LlmSettings llmSettings, TextToSpeechSettings textToSpeechSettings, MainWindowUiSettings mainWindowUiSettings, HotKeyRouterSettings hotKeyRouterSettings)
	{
		UserInstructions = userInstructions;
		AudioSettings = audioSettings;
		AzureComputerVisionSettings = azureComputerVisionSettings;
		SpeetchToTextSettings = speetchToTextSettings;
		LlmSettings = llmSettings;
		TextToSpeechSettings = textToSpeechSettings;
		MainWindowUiSettings = mainWindowUiSettings;
		HotKeyRouterSettings = hotKeyRouterSettings;
	}
}

public class AudioSettings
{
	public string ActiveCaptureDeviceFullName { get; set; }
	public string MicrophoneToggleMuteHotKey { get; set; }

	public AudioSettings()
	{
	}

	public AudioSettings(string microphoneToggleMuteHotKey)
	{
		MicrophoneToggleMuteHotKey = microphoneToggleMuteHotKey;
	}
}

public class MainWindowUiSettings
{
	public Point WindowLocation { get; set; }
	public Size WindowSize { get; set; }

	public MainWindowUiSettings()
	{
	}

	public MainWindowUiSettings(Point windowLocation, Size windowSize)
	{
		WindowLocation = windowLocation;
		WindowSize = windowSize;
	}
}

public class AzureComputerVisionSettings
{
	public string ScreenshotHotKey { get; set; }
	public string ScreenshotOcrHotKey { get; set; }
	public string OcrHotKey { get; set; }
	public string ApiKey { get; set; }
	public string Endpoint { get; set; }

	public AzureComputerVisionSettings()
	{
	}

	public AzureComputerVisionSettings(string screenshotHotKey, string screenshotOcrHotKey, string ocrHotKey, string apiKey, string endpoint)
	{
		ScreenshotHotKey = screenshotHotKey;
		ScreenshotOcrHotKey = screenshotOcrHotKey;
		OcrHotKey = ocrHotKey;
		ApiKey = apiKey;
		Endpoint = endpoint;
	}
}

public class SpeetchToTextSettings
{
	public SpeechToTextServices Service { get; set; }
	public string SpeechToTextHotKey { get; set; }
	public string ApiKey { get; set; }
	public string BaseDomain { get; set; }
	public string ModelId { get; set; }
	public string TempDirectory { get; set; }
	public string SpeechToTextPrompt { get; set; }

	public SpeetchToTextSettings()
	{
	}

	public SpeetchToTextSettings(SpeechToTextServices service, string speechToTextHotKey, string apiKey, string baseDomain, string modelId, string tempDirectory, string speechToTextPrompt)
	{
		Service = service;
		SpeechToTextHotKey = speechToTextHotKey;
		ApiKey = apiKey;
		BaseDomain = baseDomain;
		ModelId = modelId;
		TempDirectory = tempDirectory;
		SpeechToTextPrompt = speechToTextPrompt;
	}
}

public class LlmSettings
{
	public string ApiKey { get; set; }
	public string ResourceName { get; set; }
	public List<ModelDeploymentIdMap> ModelDeploymentIdMaps { get; set; }
	public List<TranscriptFormatRule> TranscriptFormatRules { get; set; }
	public string FormatTranscriptPrompt { get; set; }
	public string ReviewTranscriptPrompt { get; set; }

	public LlmSettings()
	{
		ModelDeploymentIdMaps = new List<ModelDeploymentIdMap>();
		TranscriptFormatRules = new List<TranscriptFormatRule>();
	}

	public LlmSettings(string apiKey, string resourceName, List<ModelDeploymentIdMap> modelDeploymentIdMaps, List<TranscriptFormatRule> transcriptFormatRules, string formatTranscriptPrompt, string reviewTranscriptPrompt)
	{
		ApiKey = apiKey;
		ResourceName = resourceName;
		ModelDeploymentIdMaps = modelDeploymentIdMaps;
		TranscriptFormatRules = transcriptFormatRules;
		FormatTranscriptPrompt = formatTranscriptPrompt;
		ReviewTranscriptPrompt = reviewTranscriptPrompt;
	}

	public class ModelDeploymentIdMap
	{
		public string ModelName { get; set; }
		public string DeploymentId { get; set; }

		public ModelDeploymentIdMap()
		{
		}

		public ModelDeploymentIdMap(string modelName, string deploymentId)
		{
			ModelName = modelName;
			DeploymentId = deploymentId;
		}
	}

	public class TranscriptFormatRule
	{
		public string Find { get; set; }
		public string ReplaceWith { get; set; }
		public bool CaseSensitive { get; set; }
		public MatchTypeEnum MatchType { get; set; }

		public TranscriptFormatRule()
		{
		}

		public TranscriptFormatRule(string find, string replaceWith, bool caseSensitive, MatchTypeEnum matchType)
		{
			Find = find;
			ReplaceWith = replaceWith;
			CaseSensitive = caseSensitive;
			MatchType = matchType;
		}

		public enum MatchTypeEnum
		{
			Plain = 1,
			RegEx = 2,
			Smart = 3,
		}
	}
}

public class TextToSpeechSettings
{
	public string TextToSpeechHotKey { get; set; }

	public TextToSpeechSettings()
	{
	}

	public TextToSpeechSettings(string textToSpeechHotKey)
	{
		TextToSpeechHotKey = textToSpeechHotKey;
	}
}

public class HotKeyRouterSettings
{
	public List<HotKeyRouterMap> Mappings { get; set; } = new List<HotKeyRouterMap>();

	public HotKeyRouterSettings()
	{
	}

	public HotKeyRouterSettings(List<HotKeyRouterMap> mappings)
	{
		Mappings = mappings;
	}

	public class HotKeyRouterMap
	{
		public string FromHotKey { get; set; }
		public string ToHotKey { get; set; }

		public HotKeyRouterMap(
			string fromHotKey,
			string toHotKey)
		{
			FromHotKey = fromHotKey ?? throw new ArgumentNullException(nameof(fromHotKey));
			ToHotKey = toHotKey ?? throw new ArgumentNullException(nameof(toHotKey));
		}
	}
}
