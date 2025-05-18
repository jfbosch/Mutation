using Newtonsoft.Json;
using CognitiveSupport;

namespace Mutation.Ui.Services;

public class SettingsManager : ISettingsManager
{
    public string SettingsFilePath { get; }

    public SettingsManager(string settingsFilePath)
    {
        SettingsFilePath = settingsFilePath;
    }

    public Settings LoadAndEnsureSettings()
    {
        if (!File.Exists(SettingsFilePath))
        {
            var settings = new Settings();
            SaveSettingsToFile(settings);
            return settings;
        }

        string json = File.ReadAllText(SettingsFilePath);
        return JsonConvert.DeserializeObject<Settings>(json) ?? new Settings();
    }

    public void SaveSettingsToFile(Settings settings)
    {
        string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        File.WriteAllText(SettingsFilePath, json);
    }
}
