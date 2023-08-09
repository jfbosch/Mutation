using CSCore;
using CSCore.Codecs;
using CSCore.Codecs.AAC;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using System.IO;
using System;
using System.Data;
using System.IO;
using CSCore;
using CSCore.Codecs.WAV;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.Streams;
using CSCore.MediaFoundation;

namespace CognitiveSupport
{
	public class SpeechToTextService
	{
		private readonly string ApiKey;
		private readonly string Endpoint;
		private readonly string DirectoryPath;
		private WasapiCapture SoundIn;
		private SoundInSource SoundInSource;
		private IWaveSource ConvertedSource;
		private WaveWriter WaveWriter;
		private int selectedMicDeviceIndex;
		private readonly object _lock = new object();

		public SpeechToTextService(
			string apiKey,
			string endpoint,
			string directoryPath,
			int selectedMicDeviceIndex)
		{
			ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
			Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
			DirectoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));

			if (!Directory.Exists(DirectoryPath))
			{
				Directory.CreateDirectory(DirectoryPath);
			}

			this.selectedMicDeviceIndex = selectedMicDeviceIndex;
		}

		public void StartRecording(int sampleRate = 44100, int bitsPerSample = 16, int channels = 1)
		{
			lock (_lock)
			{
				DataFlow dataFlow = DataFlow.Capture;

				//MMDeviceCollection devices = MMDeviceEnumerator.EnumerateDevices(dataFlow, DeviceState.Active);
				MMDeviceEnumerator deviceEnum = new MMDeviceEnumerator();
				MMDeviceCollection devices = deviceEnum.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active);
				if (!devices.Any())
				{
					throw new Exception($"{nameof(SpeechToTextService)} No active capture devices found.");
				}
				using (var capture = new WasapiCapture())
				{
					capture.Device = deviceEnum.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
					capture.Initialize();
				}

				var device = devices[selectedMicDeviceIndex];

				SoundIn = new WasapiCapture();
				SoundIn.Initialize();

				SoundInSource = new SoundInSource(SoundIn) { FillWithZeros = false };

				ConvertedSource = SoundInSource
					 .ChangeSampleRate(sampleRate)
					 .ToSampleSource()
					 .ToWaveSource(bitsPerSample);

				ConvertedSource = channels == 1 ? ConvertedSource.ToMono() : ConvertedSource.ToStereo();

				string fileName = Path.Combine(DirectoryPath, "out.m4a");
				WaveWriter = new WaveWriter(fileName, ConvertedSource.WaveFormat);

				SoundInSource.DataAvailable += (s, e) =>
				{
					byte[] buffer = new byte[ConvertedSource.WaveFormat.BytesPerSecond / 2];
					int read;

					while ((read = ConvertedSource.Read(buffer, 0, buffer.Length)) > 0)
					{
						WaveWriter.Write(buffer, 0, read);
					}
				};

				SoundIn.Start();
			}
		}

		public void StopRecording()
		{
			lock (_lock)
			{
				SoundIn?.Stop();
				WaveWriter?.Dispose();
				ConvertedSource?.Dispose();
				SoundInSource?.Dispose();
				SoundIn?.Dispose();
			}
		}


		public void RecordToFile(
			string filePath
			, int durationInSeconds)
		{
			//using (WasapiCapture capture = new WasapiCapture())
			//{
			//	capture.Initialize();
			//	IWaveSource source = new SoundInSource(capture) { FillWithZeros = false };
			//	using (MediaFoundationEncoder encoder = MediaFoundationEncoder.CreateMP3Encoder(source.WaveFormat, filePath))
			//	{
			//		capture.Start();
			//		byte[] buffer = new byte[source.WaveFormat.BytesPerSecond];
			//		for (int i = 0; i < durationInSeconds; i++)
			//		{
			//			int read = source.Read(buffer, 0, buffer.Length);
			//			encoder.Write(buffer, 0, read);
			//			Thread.Sleep(1000);
			//		}
			//		capture.Stop();
			//	}
			//}


			using (var wasapiCapture = new WasapiLoopbackCapture())
			{
				wasapiCapture.Initialize();
				var wasapiCaptureSource = new SoundInSource(wasapiCapture);
				using (var stereoSource = wasapiCaptureSource.ToStereo())
				{
					using (var writer = new WaveWriter("output.wav", stereoSource.WaveFormat))
					{
						byte[] buffer = new byte[stereoSource.WaveFormat.BytesPerSecond];
						wasapiCaptureSource.DataAvailable += (s, e) =>
						{
							int read = stereoSource.Read(buffer, 0, buffer.Length);
							writer.Write(buffer, 0, read);
						};

						wasapiCapture.Start();

						Thread.Sleep(3000);

						wasapiCapture.Stop();
					}
				}
			}

		}


	}
}
