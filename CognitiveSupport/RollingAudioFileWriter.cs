using NAudio.Lame;
using NAudio.Wave;
using StringExtensionLibrary;

namespace CognitiveSupport
{
	public class RollingAudioFileWriter : IDisposable
	{
		private readonly string _tempPrefix = "temp_";

		private readonly object _lock = new();
		private DirectoryInfo _sessionDirectory;

		private volatile LameMP3FileWriter _activeMp3Writer;

		private int _fileIndex = 0;
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
				if (_fileIndex <= 0)
					timeToSwitchFiles = true;
				var elapsedFileDuration = DateTime.UtcNow - _lastFileSwitch;
				if (elapsedFileDuration >= TimeSpan.FromSeconds(1))
					timeToSwitchFiles = true;

				if (timeToSwitchFiles)
				{
					_fileIndex++;
					_lastFileSwitch = DateTime.UtcNow;
					_activeMp3Writer?.Dispose();

					int previousFileIndex = _fileIndex - 1;
					RenameTempFileToFinalClipFile(previousFileIndex);

					string tempFileName = GetTempIndexFileName(_fileIndex);
					string tempFilePath = Path.Combine(_sessionDirectory.FullName, tempFileName);

					_activeMp3Writer = new LameMP3FileWriter(tempFilePath, waveFormat, LAMEPreset.STANDARD);
					timeToSwitchFiles = false;
				}

				_activeMp3Writer.Write(buffer, offset, count);
			}
		}

		private string GetTempIndexFileName(int index)
		{
			return $"{_tempPrefix}clip_{index:0000}.mp3";
		}

		private void RenameTempFileToFinalClipFile(
			int fileIndexToRename)
		{
			string tempFileName = GetTempIndexFileName(fileIndexToRename);
			string tempFilePath = Path.Combine(_sessionDirectory.FullName, tempFileName);
			if (File.Exists(tempFilePath))
			{
				string finalFileName = tempFileName.RemovePrefix(_tempPrefix);
				string finalFilePath = Path.Combine(_sessionDirectory.FullName, finalFileName);
				File.Move(tempFilePath, finalFilePath);
			}
		}

		public void Dispose()
		{
			lock (_lock)
			{
				_activeMp3Writer?.Dispose();
				_activeMp3Writer = null;
				RenameTempFileToFinalClipFile(_fileIndex);
			}
		}
	}
}
