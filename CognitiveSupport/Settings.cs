﻿using System.Drawing;

namespace CognitiveSupport;

public class Settings
{
	public string? UserInstructions { get; set; }

	public AudioSettings? AudioSettings { get; set; }
	public AzureComputerVisionSettings? AzureComputerVisionSettings { get; set; }
        public SpeechToTextSettings? SpeechToTextSettings { get; set; }
	public LlmSettings? LlmSettings { get; set; }
	public TextToSpeechSettings? TextToSpeechSettings { get; set; }

	public MainWindowUiSettings MainWindowUiSettings { get; set; } = new MainWindowUiSettings();

	public HotKeyRouterSettings HotKeyRouterSettings { get; set; } = new HotKeyRouterSettings();

	public Settings()
	{
	}

        public Settings(string? userInstructions, AudioSettings? audioSettings, AzureComputerVisionSettings? azureComputerVisionSettings, SpeechToTextSettings? speechToTextSettings, LlmSettings? llmSettings, TextToSpeechSettings? textToSpeechSettings, MainWindowUiSettings mainWindowUiSettings, HotKeyRouterSettings hotKeyRouterSettings)
        {
                UserInstructions = userInstructions;
                AudioSettings = audioSettings;
                AzureComputerVisionSettings = azureComputerVisionSettings;
                SpeechToTextSettings = speechToTextSettings;
                LlmSettings = llmSettings;
                TextToSpeechSettings = textToSpeechSettings;
                MainWindowUiSettings = mainWindowUiSettings;
                HotKeyRouterSettings = hotKeyRouterSettings;
        }
}

public class AudioSettings
{
	private CustomBeepSettingsData? customBeepSettings;

	public string? ActiveCaptureDeviceFullName { get; set; }
	public string? MicrophoneToggleMuteHotKey { get; set; }
        // Allows users to disable microphone visualization to save CPU
        public bool EnableMicrophoneVisualization { get; set; } = true;
	public CustomBeepSettingsData? CustomBeepSettings { get => customBeepSettings; set => customBeepSettings = value; }

	public AudioSettings() { }

	public AudioSettings(string? microphoneToggleMuteHotKey)
	{
		MicrophoneToggleMuteHotKey = microphoneToggleMuteHotKey;
	}

	public class CustomBeepSettingsData
	{
		public bool UseCustomBeeps { get; set; } = false;
		public string? BeepSuccessFile { get; set; }
		public string? BeepFailureFile { get; set; }
		public string? BeepStartFile { get; set; }
		public string? BeepEndFile { get; set; }
		public string? BeepMuteFile { get; set; }
		public string? BeepUnmuteFile { get; set; }

		// Helper to resolve audio file paths relative to the executable directory
		public string ResolveAudioFilePath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return path;
			if (Path.IsPathRooted(path))
				return path;
			// Use AppContext.BaseDirectory for the exe location
			return Path.Combine(AppContext.BaseDirectory, path);
		}

	}
}

public class MainWindowUiSettings
{
        public Point WindowLocation { get; set; }
        public Size WindowSize { get; set; }
        public int MaxTextBoxLineCount { get; set; } = 5;

        public MainWindowUiSettings()
        {
        }

        public MainWindowUiSettings(Point windowLocation, Size windowSize, int maxTextBoxLineCount = 5)
        {
                WindowLocation = windowLocation;
                WindowSize = windowSize;
                MaxTextBoxLineCount = maxTextBoxLineCount;
        }
}

public class AzureComputerVisionSettings
{
	public bool InvertScreenshot { get; set; }
	public string? ScreenshotHotKey { get; set; }
	public string? ScreenshotOcrHotKey { get; set; }
	public string? ScreenshotLeftToRightTopToBottomOcrHotKey { get; set; }
	public string? OcrHotKey { get; set; }
	public string? OcrLeftToRightTopToBottomHotKey { get; set; }

	// If this is not null, this hotkey will be sent to the system after an OCR operation completes. 
        public string? SendHotkeyAfterOcrOperation { get; set; }

	public string? ApiKey { get; set; }
	public string? Endpoint { get; set; }
	public int TimeoutSeconds { get; set; } = 10;

	public AzureComputerVisionSettings()
	{
	}
}

public class SpeechToTextSettings
{
        public string? TempDirectory { get; set; }
        public string? SpeechToTextHotKey { get; set; }

        // If this is not null, this hotkey will be sent to the system after a transcription operation completes.
        public string? SendHotkeyAfterTranscriptionOperation { get; set; }

        public SpeechToTextServiceSettings[]? Services { get; set; }
        public string? ActiveSpeechToTextService { get; set; }
}

public class SpeechToTextServiceSettings
{
        public string? Name { get; set; }
	public SpeechToTextProviders Provider { get; set; }
	public string? ApiKey { get; set; }
	public string? BaseDomain { get; set; }
	public string? ModelId { get; set; }
	public string? SpeechToTextPrompt { get; set; }
	public int TimeoutSeconds { get; set; } = 10;
}

public class LlmSettings
{
	public string? ApiKey { get; set; }
	public string? ResourceName { get; set; }
	public List<ModelDeploymentIdMap> ModelDeploymentIdMaps { get; set; }
	public List<TranscriptFormatRule> TranscriptFormatRules { get; set; }
	public string? FormatTranscriptPrompt { get; set; }


	public LlmSettings()
	{
		ModelDeploymentIdMaps = new List<ModelDeploymentIdMap>();
		TranscriptFormatRules = new List<TranscriptFormatRule>();
	}

	public LlmSettings(string? apiKey, string? resourceName, List<ModelDeploymentIdMap> modelDeploymentIdMaps, List<TranscriptFormatRule> transcriptFormatRules, string? formatTranscriptPrompt)
	{
		ApiKey = apiKey;
		ResourceName = resourceName;
		ModelDeploymentIdMaps = modelDeploymentIdMaps;
		TranscriptFormatRules = transcriptFormatRules;
		FormatTranscriptPrompt = formatTranscriptPrompt;
	}

	public class ModelDeploymentIdMap
	{
		public string? ModelName { get; set; }
		public string? DeploymentId { get; set; }

		public ModelDeploymentIdMap()
		{
		}

		public ModelDeploymentIdMap(string? modelName, string? deploymentId)
		{
			ModelName = modelName;
			DeploymentId = deploymentId;
		}
	}

	public class TranscriptFormatRule
	{
		public string? Find { get; set; }
		public string? ReplaceWith { get; set; }
		public bool CaseSensitive { get; set; }
		public MatchTypeEnum MatchType { get; set; }

		public TranscriptFormatRule()
		{
		}

		public TranscriptFormatRule(string? find, string? replaceWith, bool caseSensitive, MatchTypeEnum matchType)
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
	public string? TextToSpeechHotKey { get; set; }

	public TextToSpeechSettings()
	{
	}

	public TextToSpeechSettings(string? textToSpeechHotKey)
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
		public string? FromHotKey { get; set; }
		public string? ToHotKey { get; set; }

		public HotKeyRouterMap(
			string? fromHotKey,
			string? toHotKey)
		{
			FromHotKey = fromHotKey ?? throw new ArgumentNullException(nameof(fromHotKey));
			ToHotKey = toHotKey ?? throw new ArgumentNullException(nameof(toHotKey));
		}
	}
}
