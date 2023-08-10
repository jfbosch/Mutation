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
			waveIn = new WaveInEvent();
			waveIn.DeviceNumber = captureDeviceIndex;
			
			// Debugging exception to show the name.
			//throw new Exception("Device Index " + captureDeviceIndex + "   " + WaveInEvent.GetCapabilities(captureDeviceIndex).ProductName);

			mp3Writer = new LameMP3FileWriter(outputFile, waveIn.WaveFormat, LAMEPreset.STANDARD);

			waveIn.DataAvailable += (sender, e) =>
			{
				mp3Writer.Write(e.Buffer, 0, e.BytesRecorded);
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