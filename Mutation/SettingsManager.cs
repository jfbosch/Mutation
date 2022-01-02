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
		if (!File.Exists(fullPath))
		{
			throw new Exception($"Could not find the settings file:{Environment.NewLine}{Environment.NewLine}{fullPath}{Environment.NewLine}{Environment.NewLine}It has now been created. Populate it with valid settings and restart the app.");
		}
		return null;
	}
}