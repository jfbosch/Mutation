using NAudio.Lame;
using NAudio.Wave;

namespace CognitiveSupport;

public static class AudioFileConverter
{
	public static string ConvertMp4ToMp3(string inputMp4Path)
	{
		if (string.IsNullOrWhiteSpace(inputMp4Path))
			throw new ArgumentException("Input path cannot be empty", nameof(inputMp4Path));

		if (!File.Exists(inputMp4Path))
			throw new FileNotFoundException("Input file not found", inputMp4Path);

		string tempMp3Path = Path.ChangeExtension(Path.GetTempFileName(), ".mp3");

		try
		{
			using var reader = new MediaFoundationReader(inputMp4Path);
			using var writer = new LameMP3FileWriter(tempMp3Path, reader.WaveFormat, LAMEPreset.STANDARD);
			reader.CopyTo(writer);
			return tempMp3Path;
		}
		catch
		{
			if (File.Exists(tempMp3Path))
			{
				try { File.Delete(tempMp3Path); } catch { }
			}
			throw;
		}
	}

	public static bool IsVideoFile(string filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath)) return false;
		string ext = Path.GetExtension(filePath);
		return string.Equals(ext, ".mp4", StringComparison.OrdinalIgnoreCase) ||
			   string.Equals(ext, ".avi", StringComparison.OrdinalIgnoreCase) ||
			   string.Equals(ext, ".mkv", StringComparison.OrdinalIgnoreCase) ||
			   string.Equals(ext, ".mov", StringComparison.OrdinalIgnoreCase) ||
			   string.Equals(ext, ".wmv", StringComparison.OrdinalIgnoreCase) ||
			   string.Equals(ext, ".m4v", StringComparison.OrdinalIgnoreCase);
	}
}
