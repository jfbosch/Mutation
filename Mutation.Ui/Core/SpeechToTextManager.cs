using CognitiveSupport;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Mutation.Ui;

internal class SpeechToTextManager
{
	private readonly Settings _settings;
	private readonly SpeechToTextState _state;
	private CognitiveSupport.AudioRecorder? _audioRecorder;

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
                catch (Exception)
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
