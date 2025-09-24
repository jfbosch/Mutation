using System.Speech.Synthesis;

namespace CognitiveSupport
{
	public class TextToSpeechService : ITextToSpeechService
	{
		private readonly object _lock = new object();

		public void SpeakText(
			string text)
		{
			using (SpeechSynthesizer synth = new SpeechSynthesizer())
			{
				synth.SetOutputToDefaultAudioDevice();
				synth.Rate = 8;

				synth.Speak(text);

			}

		}
	}
}
