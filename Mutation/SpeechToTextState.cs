using CognitiveSupport;

namespace Mutation
{
	internal class SpeechToTextState
	{
		private Func<AudioRecorder> GetAudioRecorder;

		internal bool RecordingAudio => GetAudioRecorder() != null;
		internal bool TranscribingAudio => TranscriptionCancellationTokenSource != null;
		internal CancellationTokenSource? TranscriptionCancellationTokenSource { get; set; }

		public SpeechToTextState(
			Func<AudioRecorder> getAudioRecorder)
		{
			GetAudioRecorder = getAudioRecorder ?? throw new ArgumentNullException(nameof(getAudioRecorder));
		}

		internal void StartTranscription()
		{
			this.TranscriptionCancellationTokenSource = new();
		}

		internal void StopTranscription()
		{
			if (this.TranscriptionCancellationTokenSource is not null)
				this.TranscriptionCancellationTokenSource.Cancel();
			this.TranscriptionCancellationTokenSource = null;
		}


	}
}
