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

		if (settings.AzureComputerVisionSettings is null)
		{
			settings.AzureComputerVisionSettings = new AzureComputerVisionSettings();
			somethingWasMissing = true;
		}

		{
			var x = settings.AzureComputerVisionSettings;
			if (string.IsNullOrWhiteSpace(x.SubscriptionKey))
			{
				x.SubscriptionKey = Placeholder;
				somethingWasMissing = true;
			}

			if (string.IsNullOrWhiteSpace(x.Endpoint))
			{
				x.Endpoint = Placeholder;
				somethingWasMissing = true;
			}
		}

		if (settings.AudioSettings is null)
		{
			settings.AudioSettings = new AudioSettings();
			somethingWasMissing = true;
		}

		{
			var x = settings.AudioSettings;
			if (string.IsNullOrWhiteSpace(x.MicrophoneToggleMuteHotKey))
			{
				x.MicrophoneToggleMuteHotKey = "ALT+Q";
				somethingWasMissing = true;
			}
		}

		return somethingWasMissing;
	}

	public void SaveSettingsToFile(Settings settings)
	{
		string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
		File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
	}
}