using AudioSwitcher.AudioApi.CoreAudio;
using CognitiveSupport;

namespace Mutation.Ui;

internal class SpeechToTextServiceComboItem
{
	public SpeetchToTextServiceSettings SpeetchToTextServiceSettings { get; set; }
	public ISpeechToTextService SpeechToTextService { get; set; }
	public string Display =>
		$"{SpeetchToTextServiceSettings.Name}";

	public override string ToString()
	{
		return this.Display;
	}
}
