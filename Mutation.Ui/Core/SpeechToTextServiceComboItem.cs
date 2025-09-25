using CognitiveSupport;

namespace Mutation.Ui;

internal class SpeechToTextServiceComboItem
{
        public SpeechToTextServiceSettings SpeechToTextServiceSettings { get; set; }
        public ISpeechToTextService SpeechToTextService { get; set; }
        public string Display =>
                $"{SpeechToTextServiceSettings.Name}";

	public override string ToString()
	{
		return this.Display;
	}
}
