using CognitiveSupport;

namespace Mutation;

public interface ISettingsManager
{
	void SaveSettingsToFile(Settings settings);
	void UpgradeSettings();
	Settings LoadAndEnsureSettings();
}