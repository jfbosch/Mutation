using NAudio.Wave;
using NAudio.Lame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CognitiveSupport
{
	public class AudioRecorder : IDisposable
	{
		private WaveInEvent waveIn;
		private LameMP3FileWriter mp3Writer;

		public void StartRecording(
			int captureDeviceIndex,
			string outputFile)
		{
			try
			{
				waveIn = new WaveInEvent();
				waveIn.DeviceNumber = captureDeviceIndex;

				// It's good practice to set WaveFormat before creating LameMP3FileWriter
				// if the writer depends on it, though NAudio might handle defaults.
				// For now, assuming WaveInEvent default format is acceptable for LAME.
				// waveIn.WaveFormat = new WaveFormat(44100, 1); // Example: 44.1 kHz, Mono

				mp3Writer = new LameMP3FileWriter(outputFile, waveIn.WaveFormat, LAMEPreset.STANDARD);

				waveIn.DataAvailable += (sender, e) =>
				{
					// Check if mp3Writer is still valid, as DataAvailable might be called
					// concurrently with StopRecording/Dispose in some edge cases.
					mp3Writer?.Write(e.Buffer, 0, e.BytesRecorded);
				};

				waveIn.RecordingStopped += (sender, e) =>
				{
					// This ensures resources are cleaned up when recording stops,
					// whether normally or due to an error.
					Dispose();
				};

				waveIn.StartRecording();
			}
			catch (Exception)
			{
				// If any part of the setup fails, ensure cleanup.
				Dispose();
				throw; // Re-throw the original exception to signal failure.
			}
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