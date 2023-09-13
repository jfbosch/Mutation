using NAudio.Lame;
using NAudio.Wave;

namespace CognitiveSupport
{
	public class RollingAudioFileWriter : IDisposable
	{
		private readonly object _lock = new();
		private DirectoryInfo _sessionDirectory;

		private volatile LameMP3FileWriter _activeMp3Writer;

		private int _fileCounter = 0;
		private DateTime _lastFileSwitch = DateTime.UtcNow;

		public RollingAudioFileWriter(
			DirectoryInfo outputDirectory)
		{
			_sessionDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
			string session = $"Session_{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}";
			_sessionDirectory = new DirectoryInfo(Path.Combine(_sessionDirectory.FullName, session));
			if (!_sessionDirectory.Exists)
				_sessionDirectory.Create();

		}

		public void Write(
			byte[] buffer,
			int offset,
			int count,
			WaveFormat waveFormat)
		{
			//BookMark??
			//if (buffer is null)
			//	throw new ArgumentNullException(nameof(buffer));

			lock (_lock)
			{
				bool timeToSwitchFiles = false;
				if (_fileCounter <= 0)
					timeToSwitchFiles = true;
				var elapsedFileDuration = DateTime.UtcNow - _lastFileSwitch;
				if (elapsedFileDuration >= TimeSpan.FromSeconds(1))
					timeToSwitchFiles = true;

				if (timeToSwitchFiles)
				{
					_fileCounter++;
					_lastFileSwitch = DateTime.UtcNow;
					_activeMp3Writer?.Dispose();
					string file = Path.Combine(_sessionDirectory.FullName, $"clip_{_fileCounter:0000}.mp3");
					_activeMp3Writer = new LameMP3FileWriter(file, waveFormat, LAMEPreset.STANDARD);
					timeToSwitchFiles = false;
				}

				_activeMp3Writer.Write(buffer, offset, count);
			}
		}

		public void Dispose()
		{
			lock (_lock)
			{
				_activeMp3Writer?.Dispose();
				_activeMp3Writer = null;
			}
		}
	}
}
