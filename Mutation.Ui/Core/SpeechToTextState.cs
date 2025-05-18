using CognitiveSupport;

namespace Mutation.Ui
{
	internal class SpeechToTextState
	{
		internal SemaphoreSlim AudioRecorderLock { get; } = new SemaphoreSlim(1, 1);

		private Func<AudioRecorder> GetAudioRecorder;
		internal bool RecordingAudio => GetAudioRecorder() != null;

		internal CancellationTokenSource? TranscriptionCancellationTokenSource { get; set; }
		internal bool TranscribingAudio => TranscriptionCancellationTokenSource != null;

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
