using System.Speech.Synthesis;

namespace CognitiveSupport
{
	public class TextToSpeechService
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

				//var voices = synth.GetInstalledVoices();
				//string voiceNames = "";
				//foreach (var voice in voices)
				//	voiceNames += voice.VoiceInfo.Name + Environment.NewLine;
				//MessageBox.Show(voiceNames, "Available Voices");
			}

		}
	}
}