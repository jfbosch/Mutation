using AudioSwitcher.AudioApi.CoreAudio;
using CognitiveSupport;

namespace Mutation;

internal class SpeechToTextServiceComboItem
{
	public SpeetchToTextService SpeetchToTextService { get; set; }
	public string Display =>
		$"{SpeetchToTextService.Name}";

	public override string ToString()
	{
		return this.Display;
	}
}
