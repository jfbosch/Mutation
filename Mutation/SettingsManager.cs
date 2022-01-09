using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace Mutation;

internal class SettingsManager
{
	private string SettingsFilePath { get; set; }

	public SettingsManager(
		string settingsFilePath)
	{
		SettingsFilePath = settingsFilePath;
	}

	internal Settings LoadSettings()
	{
		string fullPath = Path.GetFullPath(SettingsFilePath);
		CreateSettingsFileOfNotExists(fullPath);

		string json = File.ReadAllText(fullPath);
		Settings settings = JsonConvert.DeserializeObject<Settings>(json);

		return settings;
	}

	private void CreateSettingsFileOfNotExists(string fullPath)
	{
		if (!File.Exists(fullPath))
		{
			var settings = new Settings
			{
				UserInstructions = $"Populate this Mutation settings file with valid settings, save, and restart the app: {fullPath}",
				AzureComputerVisionSettings = new(),
			};

			string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
			File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
			Process.Start("notepad.exe", SettingsFilePath);
		}
	}
}