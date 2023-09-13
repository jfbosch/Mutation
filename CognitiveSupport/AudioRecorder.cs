using NAudio.Wave;

namespace CognitiveSupport
{
	public class AudioRecorder : IDisposable
	{
		public Exception LastRecordingException { get; set; }

		private WaveInEvent waveIn;
		private RollingAudioFileWriter mp3Writer;

		public void StartRecording(
			int captureDeviceIndex,
			DirectoryInfo outputDirectory)
		{
			LastRecordingException = null;

			waveIn = new WaveInEvent();
			waveIn.DeviceNumber = captureDeviceIndex;

			// Debugging exception to show the name.
			//throw new Exception("Device Index " + captureDeviceIndex + "   " + WaveInEvent.GetCapabilities(captureDeviceIndex).ProductName);

			mp3Writer = new RollingAudioFileWriter(outputDirectory);

			waveIn.DataAvailable += (sender, e) =>
			{
				try
				{
					mp3Writer.Write(e.Buffer, 0, e.BytesRecorded, waveIn.WaveFormat);
				}
				catch (Exception ex)
				{
					LastRecordingException = ex;
					StopRecording();
				}
			};

			waveIn.RecordingStopped += (sender, e) =>
			{
				Dispose();
			};

			waveIn.StartRecording();
		}

		public void StopRecording()
		{
			if (waveIn != null)
			{
				waveIn.StopRecording();
			}
		}

		public void Dispose()
		{
			mp3Writer?.Dispose();
			mp3Writer = null;
			waveIn?.Dispose();
			waveIn = null;
		}
	}

}