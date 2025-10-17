using CognitiveSupport;
using NAudio.Lame;
using NAudio.Vorbis;
using NAudio.Wave;
using Concentus.Oggfile;
using Concentus.Structs;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mutation.Ui;

internal class SpeechToTextManager
{
	private readonly Settings _settings;
	private readonly SpeechToTextState _state;
	private CognitiveSupport.AudioRecorder? _audioRecorder;
	private static readonly byte[] OpusHeadSignature = Encoding.ASCII.GetBytes("OpusHead");

	public SpeechToTextManager(Settings settings)
	{
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));
		_state = new SpeechToTextState(() => _audioRecorder);
	}

	public bool Recording => _state.RecordingAudio;
	public bool Transcribing => _state.TranscribingAudio;

        private string SessionsDirectory => Path.Combine(_settings.SpeechToTextSettings!.TempDirectory!, Constants.SessionsDirectoryName);
        private string AudioFilePath => Path.Combine(SessionsDirectory, "mutation_recording.mp3");

        public bool HasRecordedAudio() => TryGetLatestRecording(out _);

        public bool TryGetLatestRecording(out string path)
        {
                path = AudioFilePath;
                if (!File.Exists(path))
                        return false;

                try
                {
                        var info = new FileInfo(path);
                        return info.Length > 0;
                }
                catch (UnauthorizedAccessException)
                {
                        return false;
                }
                catch (DirectoryNotFoundException)
                {
                        return false;
                }
                catch (PathTooLongException)
                {
                        return false;
                }
                catch (IOException)
                {
                        return false;
                }
        }

	public async Task StartRecordingAsync(int microphoneDeviceIndex)
	{
		Directory.CreateDirectory(SessionsDirectory);
		await _state.AudioRecorderLock.WaitAsync().ConfigureAwait(false);
		try
		{
			_audioRecorder = new CognitiveSupport.AudioRecorder();
			_audioRecorder.StartRecording(microphoneDeviceIndex, AudioFilePath);
		}
		finally
		{
			_state.AudioRecorderLock.Release();
		}
	}

        public async Task<string> StopRecordingAndTranscribeAsync(CognitiveSupport.ISpeechToTextService service, string prompt, CancellationToken token)
        {
                if (service is null)
                        throw new ArgumentNullException(nameof(service));

		await _state.AudioRecorderLock.WaitAsync(token).ConfigureAwait(false);
		try
		{
			_audioRecorder?.StopRecording();
			_audioRecorder?.Dispose();
			_audioRecorder = null;

			string text = string.Empty;
			_state.StartTranscription();
			try
			{
				text = await service.ConvertAudioToText(prompt, AudioFilePath, _state.TranscriptionCancellationTokenSource!.Token).ConfigureAwait(false);
			}
			finally
			{
				_state.StopTranscription();
			}
			return text;
		}
		finally
		{
                        _state.AudioRecorderLock.Release();
                }
        }

        
	public async Task<string> SaveUploadedAudioAsync(string sourceFilePath, CancellationToken token)
	{
		if (string.IsNullOrWhiteSpace(sourceFilePath))
			throw new ArgumentException("Source file path must be provided.", nameof(sourceFilePath));

		Directory.CreateDirectory(SessionsDirectory);

		await _state.AudioRecorderLock.WaitAsync(token).ConfigureAwait(false);
		try
		{
			if (_audioRecorder != null)
				throw new InvalidOperationException("Recording is currently in progress.");

			if (!File.Exists(sourceFilePath))
				throw new FileNotFoundException("Uploaded audio file could not be found.", sourceFilePath);

			string destination = AudioFilePath;

			string sourceFullPath = Path.GetFullPath(sourceFilePath);
			string destinationFullPath = Path.GetFullPath(destination);

			if (string.Equals(sourceFullPath, destinationFullPath, StringComparison.OrdinalIgnoreCase))
				return destination;

                        try
                        {
                                if (string.Equals(Path.GetExtension(sourceFilePath), ".mp3", StringComparison.OrdinalIgnoreCase))
                                {
                                        File.Copy(sourceFilePath, destination, overwrite: true);
                                }
                                else
                                {
                                        using var reader = CreateWaveReaderForUpload(sourceFilePath);
                                        using var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
                                        using var writer = new LameMP3FileWriter(destination, pcmStream.WaveFormat, LAMEPreset.STANDARD);
                                        pcmStream.CopyTo(writer);
                                }
                        }
                        catch
			{
				try
				{
					if (File.Exists(destination))
						File.Delete(destination);
				}
				catch
				{
					// Ignore cleanup failures and throw original exception.
				}

				throw;
			}

			return destination;
		}
		finally
		{
			_state.AudioRecorderLock.Release();
		}
	}

	public async Task<string> TranscribeExistingRecordingAsync(CognitiveSupport.ISpeechToTextService service, string prompt, CancellationToken token)
        {
                if (service is null)
                        throw new ArgumentNullException(nameof(service));

                await _state.AudioRecorderLock.WaitAsync(token).ConfigureAwait(false);
                try
                {
                        if (_audioRecorder != null)
                                throw new InvalidOperationException("Recording is currently in progress.");

                        if (!TryGetLatestRecording(out var path))
                                throw new FileNotFoundException("No recording is available for transcription.", AudioFilePath);

                        string text = string.Empty;
                        _state.StartTranscription();
                        try
                        {
                                text = await service.ConvertAudioToText(prompt, path, _state.TranscriptionCancellationTokenSource!.Token).ConfigureAwait(false);
                        }
                        finally
                        {
                                _state.StopTranscription();
                        }

                        return text;
                }
                finally
                {
                        _state.AudioRecorderLock.Release();
                }
        }

        private static WaveStream CreateWaveReaderForUpload(string sourceFilePath)
        {
                try
                {
                        return new AudioFileReader(sourceFilePath);
                }
                catch (COMException ex) when (ex.HResult == unchecked((int)0xC00D36C4) && IsOggFamily(sourceFilePath))
                {
                        return CreateOggWaveReader(sourceFilePath);
                }
        }

        private static WaveStream CreateOggWaveReader(string sourceFilePath)
        {
                try
                {
                        return new VorbisWaveReader(sourceFilePath);
                }
                catch (Exception ex) when (IsVorbisInitializationFailure(ex))
                {
                        if (TryCreateOpusWaveStream(sourceFilePath, out var opusStream))
                                return opusStream;

                        throw;
                }
        }

        private static bool IsVorbisInitializationFailure(Exception ex) => ex is ArgumentException or InvalidDataException;

        private static bool TryCreateOpusWaveStream(string sourceFilePath, out WaveStream waveStream)
        {
                waveStream = null!;

                int sampleRate;
                int channelCount;

                try
                {
                        if (!TryReadOpusHeader(sourceFilePath, out sampleRate, out channelCount))
                                return false;
                }
                catch (IOException)
                {
                        return false;
                }
                catch (UnauthorizedAccessException)
                {
                        return false;
                }
                catch (NotSupportedException)
                {
                        return false;
                }

                sampleRate = sampleRate <= 0 ? 48000 : sampleRate;
                channelCount = Math.Clamp(channelCount, 1, 2);

                Span<int> candidateRates = stackalloc int[sampleRate == 48000 ? 1 : 2];
                candidateRates[0] = sampleRate;
                if (candidateRates.Length == 2)
                        candidateRates[1] = 48000;

                Span<int> candidateChannels = stackalloc int[2];
                candidateChannels[0] = channelCount;
                candidateChannels[1] = channelCount == 1 ? 2 : 1;

                int lastChannelTried = -1;

                foreach (int channelCandidate in candidateChannels)
                {
                        int normalizedChannel = Math.Clamp(channelCandidate, 1, 2);
                        if (normalizedChannel == lastChannelTried)
                                continue;

                        lastChannelTried = normalizedChannel;

                        foreach (int candidateRate in candidateRates)
                        {
                                try
                                {
                                        waveStream = CreateOpusWaveStream(sourceFilePath, candidateRate, normalizedChannel);
                                        return true;
                                }
                                catch (ArgumentOutOfRangeException)
                                {
                                        continue;
                                }
                                catch (InvalidDataException)
                                {
                                        continue;
                                }
                        }
                }

                waveStream = null!;
                return false;
        }

        private static bool TryReadOpusHeader(string sourceFilePath, out int sampleRate, out int channelCount)
        {
                sampleRate = 0;
                channelCount = 0;

                byte[] headerBuffer = new byte[256];

                using FileStream stream = File.OpenRead(sourceFilePath);
                int bytesRead = stream.Read(headerBuffer, 0, headerBuffer.Length);
                if (bytesRead < 19)
                        return false;

                ReadOnlySpan<byte> signature = OpusHeadSignature;
                for (int index = 0; index <= bytesRead - signature.Length - 11; index++)
                {
                        if (!MatchesSignature(headerBuffer.AsSpan(index), signature))
                                continue;

                        int channelIndex = index + signature.Length + 1;
                        int sampleRateIndex = channelIndex + 3;

                        if (channelIndex >= bytesRead || sampleRateIndex + 3 >= bytesRead)
                                return false;

                        channelCount = headerBuffer[channelIndex];
                        sampleRate = BitConverter.ToInt32(headerBuffer, sampleRateIndex);
                        return channelCount > 0;
                }

                return false;
        }

        private static bool MatchesSignature(ReadOnlySpan<byte> source, ReadOnlySpan<byte> signature)
        {
                if (source.Length < signature.Length)
                        return false;

                for (int i = 0; i < signature.Length; i++)
                {
                        if (source[i] != signature[i])
                                return false;
                }

                return true;
        }

        private static WaveStream CreateOpusWaveStream(string sourceFilePath, int sampleRate, int channelCount)
        {
                MemoryStream pcmBuffer = new MemoryStream();
                WaveFormat waveFormat = new WaveFormat(sampleRate, 16, channelCount);

                try
                {
                        using FileStream fileStream = File.OpenRead(sourceFilePath);
                        using OpusOggReadStream opusStream = new OpusOggReadStream(OpusDecoder.Create(sampleRate, channelCount), fileStream);
                        using BinaryWriter writer = new BinaryWriter(pcmBuffer, Encoding.UTF8, leaveOpen: true);

                        while (opusStream.HasNextPacket)
                        {
                                short[]? packet = opusStream.DecodeNextPacket();
                                if (packet is null || packet.Length == 0)
                                        continue;

                                foreach (short sample in packet)
                                        writer.Write(sample);
                        }

                        writer.Flush();

                        if (pcmBuffer.Length == 0)
                                throw new InvalidDataException("Opus stream contained no decodable packets.");

                        pcmBuffer.Position = 0;
                        return new RawSourceWaveStream(pcmBuffer, waveFormat);
                }
                catch
                {
                        pcmBuffer.Dispose();
                        throw;
                }
        }

        private static bool IsOggFamily(string sourceFilePath)
        {
                string extension = Path.GetExtension(sourceFilePath);
                if (string.IsNullOrEmpty(extension))
                        return false;

                return extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".oga", StringComparison.OrdinalIgnoreCase);
        }

        public void CancelTranscription()
        {
                _state.StopTranscription();
        }

	public async Task StopRecordingAsync()
	{
		await _state.AudioRecorderLock.WaitAsync().ConfigureAwait(false);
		try
		{
			_audioRecorder?.StopRecording();
			_audioRecorder?.Dispose();
			_audioRecorder = null;
		}
		finally
		{
			_state.AudioRecorderLock.Release();
		}
	}
}
