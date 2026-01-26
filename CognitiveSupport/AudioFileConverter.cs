using Concentus.Enums;
using Concentus.Oggfile;
using Concentus.Structs;
using NAudio.Wave;
using System;
using System.IO;

namespace CognitiveSupport;

public static class AudioFileConverter
{
	public static string ConvertMp4ToOgg(string inputMp4Path)
	{
		if (string.IsNullOrWhiteSpace(inputMp4Path))
			throw new ArgumentException("Input path cannot be empty", nameof(inputMp4Path));

		if (!File.Exists(inputMp4Path))
			throw new FileNotFoundException("Input file not found", inputMp4Path);

		string tempOggPath = Path.ChangeExtension(Path.GetTempFileName(), ".ogg");

		try
		{
			using var reader = new MediaFoundationReader(inputMp4Path);
			
			// Target format: 48kHz, Mono, 16-bit
			var outFormat = new WaveFormat(48000, 16, 1);
			
			using var resampler = new MediaFoundationResampler(reader, outFormat);
			resampler.ResamplerQuality = 60; // Reasonable quality

			using var outStream = new FileStream(tempOggPath, FileMode.Create, FileAccess.Write, FileShare.None);
			var encoder = new OpusEncoder(48000, 1, OpusApplication.OPUS_APPLICATION_VOIP)
			{
				Bitrate = 24000,
				UseVBR = true,
				UseDTX = false // DTX not supported in Ogg streams by Concentus.OggFile
			};
			var tags = new OpusTags();
			var oggStream = new OpusOggWriteStream(encoder, outStream, tags);

			// Buffer for reading from resampler (1 second worth of audio)
			byte[] buffer = new byte[outFormat.AverageBytesPerSecond];
			int bytesRead;

			// Buffer for accumulation to feed fixed frame sizes to Opus (960 samples = 1920 bytes)
			// Opus requires 2.5, 5, 10, 20, 40, or 60ms frames. We use 20ms (960 samples).
			int samplesPerFrame = 960;
			int bytesPerFrame = samplesPerFrame * 2;
			List<byte> accumulationBuffer = new List<byte>();

			while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
			{
				for (int i = 0; i < bytesRead; i++)
				{
					accumulationBuffer.Add(buffer[i]);
				}

				while (accumulationBuffer.Count >= bytesPerFrame)
				{
					byte[] frameBytes = accumulationBuffer.GetRange(0, bytesPerFrame).ToArray();
					accumulationBuffer.RemoveRange(0, bytesPerFrame);

					// Convert byte[] to short[]
					short[] pcmSamples = new short[samplesPerFrame];
					Buffer.BlockCopy(frameBytes, 0, pcmSamples, 0, bytesPerFrame);

					oggStream.WriteSamples(pcmSamples, 0, samplesPerFrame);
				}
			}

			// Handle remaining bytes (pad with silence if needed, or just finish)
			// For speech, we can probably drop the last partial frame if it's very short, 
			// or pad it. 
			if (accumulationBuffer.Count > 0)
			{
				// Padding with silence to reach frame size
				while (accumulationBuffer.Count < bytesPerFrame)
				{
					accumulationBuffer.Add(0);
				}
				
				byte[] frameBytes = accumulationBuffer.ToArray();
				short[] pcmSamples = new short[samplesPerFrame];
				Buffer.BlockCopy(frameBytes, 0, pcmSamples, 0, bytesPerFrame);
				oggStream.WriteSamples(pcmSamples, 0, samplesPerFrame);
			}

			oggStream.Finish();
			return tempOggPath;
		}
		catch
		{
			if (File.Exists(tempOggPath))
			{
				try { File.Delete(tempOggPath); } catch { }
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
