using NAudio.Lame;
using NAudio.Wave;
using StringExtensionLibrary;
using System;
using System.IO;

namespace CognitiveSupport;

public class RollingAudioFileWriter : IDisposable
{
	private readonly string _tempPrefix = "temp_";

	private readonly object _lock = new object();
	private DirectoryInfo _sessionDirectory;

	private volatile WaveFileWriter _activeWaveWriter;

	private int _fileIndex = 0;
	private DateTime _lastFileSwitch = DateTime.UtcNow;

	public RollingAudioFileWriter(DirectoryInfo outputDirectory)
	{
		_sessionDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
		string session = $"Session_{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}";
		_sessionDirectory = new DirectoryInfo(Path.Combine(_sessionDirectory.FullName, session));
		if (!_sessionDirectory.Exists)
			_sessionDirectory.Create();
	}

	public void Write(byte[] buffer, int offset, int count, WaveFormat waveFormat)
	{
		if (buffer is null)
			throw new ArgumentNullException(nameof(buffer));

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
				_activeWaveWriter?.Dispose();

				int previousFileIndex = _fileIndex - 1;
				RenameTempFileToFinalClipFile(previousFileIndex);

				string tempFileName = GetTempIndexFileName(_fileIndex);
				string tempFilePath = Path.Combine(_sessionDirectory.FullName, tempFileName);

				_activeWaveWriter = new WaveFileWriter(tempFilePath, waveFormat);
				timeToSwitchFiles = false;
			}

			_activeWaveWriter.Write(buffer, offset, count);
		}
	}

	private string GetTempIndexFileName(int index)
	{
		return $"{_tempPrefix}clip_{index:0000}.wav";
	}

	private void RenameTempFileToFinalClipFile(int fileIndexToRename)
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
			_activeWaveWriter?.Dispose();
			_activeWaveWriter = null;
			RenameTempFileToFinalClipFile(_fileIndex);
			SpliceClips();
		}
	}


	public void SpliceClips()
	{
		string outputPath = Path.Combine(_sessionDirectory.FullName, "_output.mp3");

		var waveFiles = _sessionDirectory.GetFiles("clip_*.wav").OrderBy(f => f.Name).ToList();
		if (waveFiles.Count == 0)
			return;

		using (var reader = new WaveFileReader(waveFiles[0].FullName))
		using (var mp3Writer = new LameMP3FileWriter(outputPath, reader.WaveFormat, LAMEPreset.STANDARD))
		{
			byte[] buffer = new byte[reader.WaveFormat.AverageBytesPerSecond * 4];

			foreach (var waveFile in waveFiles)
			{
				using (var waveReader = new WaveFileReader(waveFile.FullName))
				{
					int read;
					while ((read = waveReader.Read(buffer, 0, buffer.Length)) > 0)
					{
						mp3Writer.Write(buffer, 0, read);
					}
				}
			}
		}
	}

}


