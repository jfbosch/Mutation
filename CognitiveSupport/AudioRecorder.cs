using NAudio.Wave;

namespace CognitiveSupport
{
	public class AudioRecorder : IDisposable
	{
		public Exception? LastRecordingException { get; set; }

		private WaveInEvent? _waveIn;
		private RollingAudioFileWriter? _audioWriter;

		/// <returns>The session directory to which recorded files are being saved.</returns>
		public DirectoryInfo StartRecording(
			int captureDeviceIndex,
			DirectoryInfo outputDirectory)
		{
			LastRecordingException = null;

			_waveIn = new WaveInEvent();
			_waveIn.DeviceNumber = captureDeviceIndex;

			// Debugging exception to show the name.
			//throw new Exception("Device Index " + captureDeviceIndex + "   " + WaveInEvent.GetCapabilities(captureDeviceIndex).ProductName);

			_audioWriter = new RollingAudioFileWriter(outputDirectory);

			_waveIn.DataAvailable += (sender, e) =>
			{
				try
				{
					_audioWriter.Write(e.Buffer, 0, e.BytesRecorded, _waveIn.WaveFormat);
				}
				catch (Exception ex)
				{
					LastRecordingException = ex;
					StopRecording();
				}
			};

			_waveIn.RecordingStopped += (sender, e) =>
			{
				Dispose();
			};

			_waveIn.StartRecording();

			return _audioWriter.SessionDirectory;
		}

		public void StopRecording()
		{
			if (_waveIn != null)
			{
				_waveIn.StopRecording();
			}
		}

		public void Dispose()
		{
			_audioWriter?.Dispose();
			_audioWriter = null;
			_waveIn?.Dispose();
			_waveIn = null;
		}
	}

}