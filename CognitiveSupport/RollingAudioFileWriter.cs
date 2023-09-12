using NAudio.Lame;
using NAudio.Wave;

namespace CognitiveSupport
{
	public class RollingAudioFileWriter : IDisposable
	{
		private readonly object _lock = new();
		private DirectoryInfo _outputDirectory;

		private LameMP3FileWriter _mp3Writer1;
		private LameMP3FileWriter _mp3Writer2;
		private volatile LameMP3FileWriter _activeMp3Writer;

		private int _fileCounter = 0;
		private DateTime _lastFileSwitch = DateTime.UtcNow;

		public RollingAudioFileWriter(
			DirectoryInfo outputDirectory)
		{
			_outputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
		}

		public void Write(
			byte[] buffer,
			int offset,
			int count)
		{
			//BookMark??
			//if (buffer is null)
			//	throw new ArgumentNullException(nameof(buffer));

			lock (_lock)
			{
				if (_fileCounter <= 0)
				{
					_fileCounter++;
					_lastFileSwitch = DateTime.UtcNow;
				}

				var elapsedFileDuration = DateTime.UtcNow - _lastFileSwitch;
				if (elapsedFileDuration >= TimeSpan.FromSeconds(1))
				{
					_fileCounter++;
					_lastFileSwitch = DateTime.UtcNow;

					_activeMp3Writer?.Dispose();
					string file = Path.Combine(_outputDirectory.FullName, $"{_fileCounter:0000}.mp3");
					_activeMp3Writer = new LameMP3FileWriter(file, new WaveInEvent().WaveFormat, LAMEPreset.STANDARD);

				}

				_activeMp3Writer.Write(buffer, offset, count);
			}
		}

		public void Dispose()
		{
			lock (_lock)
			{
				_mp3Writer1?.Dispose();
				_mp3Writer1 = null;
				_mp3Writer2?.Dispose();
				_mp3Writer2 = null;

				// The active one is either 1 or 2, and they have already been disposed, so don't dispose the active one.
				_activeMp3Writer?.Dispose();
				_activeMp3Writer = null;
			}
		}
	}
}
