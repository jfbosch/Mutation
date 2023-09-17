using NAudio.Dsp;
using NAudio.Lame;
using NAudio.Wave;
using StringExtensionLibrary;

namespace CognitiveSupport;

public class RollingAudioFileWriter : IDisposable
{
	private readonly string _tempPrefix = "temp_";

	private readonly object _lock = new object();
	private DirectoryInfo _sessionDirectory;

	private volatile WaveFileWriter? _activeWaveWriter;

	private int _fileIndex = 0;
	private DateTime _lastFileSwitch = DateTime.UtcNow;

	public DirectoryInfo SessionDirectory => _sessionDirectory;

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
			//SpliceClips();
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

	public void Split(string waveFile)
	{
		int fftLength = 4096;
		float[] fftBuffer = new float[fftLength];
		Complex[] fftComplex = new Complex[fftLength];
		List<long> pausePositions = new List<long>();
		long samplesRead = 0;
		long currentPauseStart = 0;
		bool isPause = false;

		double[] volumeHistory = new double[10];  // Keep track of last 10 volumes
		int historyIndex = 0;

		using (var reader = new WaveFileReader(waveFile))
		{
			var sampleProvider = reader.ToSampleProvider();
			long totalSamples = reader.Length / reader.BlockAlign;

			while (samplesRead < totalSamples)
			{
				int samplesToRead = fftLength / 2;
				float[] audioBuffer = new float[samplesToRead];
				int samplesReadNow = sampleProvider.Read(audioBuffer, 0, samplesToRead);
				samplesRead += samplesReadNow;

				// Apply Hanning window
				for (int i = 0; i < samplesReadNow; i++)
				{
					audioBuffer[i] *= 0.5f * (1.0f - (float)Math.Cos(2 * Math.PI * i / (samplesReadNow - 1)));
				}

				for (int i = 0; i < fftLength / 2; i++)
				{
					fftComplex[i].X = (i < samplesReadNow) ? audioBuffer[i] : 0;
					fftComplex[i].Y = 0;
				}

				FastFourierTransform.FFT(true, (int)Math.Log(fftLength, 2.0), fftComplex);

				double sum = 0;
				for (int i = 1; i < fftComplex.Length / 2; i++)
				{
					sum += (fftComplex[i].X * fftComplex[i].X) + (fftComplex[i].Y * fftComplex[i].Y);
				}
				double average = sum / (fftComplex.Length / 2);
				double volume = Math.Sqrt(average);

				// Update volume history
				volumeHistory[historyIndex % volumeHistory.Length] = volume;
				historyIndex++;

				// Calculate dynamic threshold
				double dynamicThreshold = volumeHistory.Average() * 0.9;

				if (volume < dynamicThreshold)
				{
					if (!isPause)
					{
						currentPauseStart = samplesRead;
						isPause = true;
					}
				}
				else
				{
					if (isPause)
					{
						long pauseLength = samplesRead - currentPauseStart;
						long bufferZone = (long)(reader.WaveFormat.SampleRate * 0.02d); // 20 ms buffer

						if (pauseLength >= (reader.WaveFormat.SampleRate * 0.05) // 50 ms
								  && currentPauseStart > bufferZone
								  && (samplesRead + bufferZone) < totalSamples)
						{
							pausePositions.Add(currentPauseStart + bufferZone);
						}
						isPause = false;
					}
				}
			}

			// Create split audio files
			long lastPosition = 0;
			int splitIndex = 0;
			pausePositions.Add(totalSamples);  // To include remaining audio

			foreach (var pausePosition in pausePositions)
			{
				string outputPath = Path.Combine(_sessionDirectory.FullName, $"split_{splitIndex}.wav");
				CreateSplitWaveFile(reader, lastPosition, pausePosition, outputPath);
				lastPosition = pausePosition;
				splitIndex++;
			}
		}
	}


	private void CreateSplitWaveFile(WaveFileReader reader, long startPosition, long endPosition, string outputPath)
	{
		using (var writer = new WaveFileWriter(outputPath, reader.WaveFormat))
		{
			reader.Position = startPosition * reader.BlockAlign;
			long bytesToRead = (endPosition - startPosition) * reader.BlockAlign;
			byte[] buffer = new byte[bytesToRead];
			reader.Read(buffer, 0, (int)bytesToRead);
			writer.Write(buffer, 0, (int)bytesToRead);
		}
	}


}

