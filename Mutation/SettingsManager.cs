using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace Mutation;

internal class SettingsManager
{
	private string SettingsFilePath { get; set; }
	private string SettingsFileFullPath => Path.GetFullPath(SettingsFilePath);

	public SettingsManager(
		string settingsFilePath)
	{
		SettingsFilePath = settingsFilePath;
	}

	internal Settings LoadAndEnsureSettings()
	{
		CreateSettingsFileOfNotExists(SettingsFileFullPath);

		string json = File.ReadAllText(SettingsFileFullPath);
		Settings settings = JsonConvert.DeserializeObject<Settings>(json);

		if (EnsureSettings(settings))
		{
			SaveSettingsToFile(settings);
		}

		return settings;
	}

	private void CreateSettingsFileOfNotExists(string fullPath)
	{
		if (!File.Exists(fullPath))
		{
			var settings = new Settings();
			EnsureSettings(settings);

			SaveSettingsToFile(settings);
			Process.Start("notepad.exe", SettingsFilePath);
		}
	}

	private bool EnsureSettings(Settings settings)
	{
		const string Placeholder = "<placeholder>";

		bool somethingWasMissing = false;

		settings.UserInstructions = "Change the values of the settings below to your preferences, save the file, and restart Mutation.exe";


		if (settings.AzureComputerVisionSettings is null)
		{
			settings.AzureComputerVisionSettings = new AzureComputerVisionSettings();
			somethingWasMissing = true;
		}
		var azureComputerVisionSettings = settings.AzureComputerVisionSettings;
		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.OcrHotKey))
		{
			azureComputerVisionSettings.OcrHotKey = "ALT+J";
			somethingWasMissing = true;
		}

		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.ScreenshotHotKey))
		{
			azureComputerVisionSettings.ScreenshotHotKey = "SHIFT+ALT+K\""; 
			somethingWasMissing = true;
		}

		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.ScreenshotOcrHotKey))
		{
			azureComputerVisionSettings.ScreenshotOcrHotKey = "SHIFT+ALT+Q";
			somethingWasMissing = true;
		}
		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.SubscriptionKey))
		{
			azureComputerVisionSettings.SubscriptionKey = Placeholder;
			somethingWasMissing = true;
		}
		if (string.IsNullOrWhiteSpace(azureComputerVisionSettings.Endpoint))
		{
			azureComputerVisionSettings.Endpoint = Placeholder;
			somethingWasMissing = true;
		}


		if (settings.AudioSettings is null)
		{
			settings.AudioSettings = new AudioSettings();
			somethingWasMissing = true;
		}
		var audioSettings = settings.AudioSettings;
		if (string.IsNullOrWhiteSpace(audioSettings.MicrophoneToggleMuteHotKey))
		{
			audioSettings.MicrophoneToggleMuteHotKey = "ALT+Q";
			somethingWasMissing = true;
		}


		if (settings.OpenAiSettings is null)
		{
			settings.OpenAiSettings = new OpenAiSettings();
			somethingWasMissing = true;
		}
		var openAiSettings = settings.OpenAiSettings;
		if (string.IsNullOrWhiteSpace(openAiSettings.SpeechToTextHotKey))
		{
			openAiSettings.SpeechToTextHotKey = "SHIFT+ALT+U";
			somethingWasMissing = true;
		}
		if (string.IsNullOrWhiteSpace(openAiSettings.ApiKey))
		{
			openAiSettings.ApiKey = Placeholder;
			somethingWasMissing = true;
		}
		if (string.IsNullOrWhiteSpace(openAiSettings.Endpoint))
		{
			openAiSettings.Endpoint = Placeholder;
			somethingWasMissing = true;
		}
		if (string.IsNullOrWhiteSpace(openAiSettings.TempDirectory))
		{
			openAiSettings.TempDirectory = @"C:\Temp\Mutation";
			somethingWasMissing = true;
		}


		return somethingWasMissing;
	}

	public void SaveSettingsToFile(Settings settings)
	{
		string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
		File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
	}
}
