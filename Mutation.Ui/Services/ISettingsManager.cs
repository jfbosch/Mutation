using CognitiveSupport;

namespace Mutation.Ui.Services;

public interface ISettingsManager
{
	void SaveSettingsToFile(Settings settings);
	void UpgradeSettings();
	Settings LoadAndEnsureSettings();
}