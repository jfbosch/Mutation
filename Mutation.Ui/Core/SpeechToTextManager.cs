using CognitiveSupport;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

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

	private string SessionsDirectory => Path.Combine(_settings.SpeetchToTextSettings!.TempDirectory!, Constants.SessionsDirectoryName);
	private string AudioFilePath => Path.Combine(SessionsDirectory, "mutation_recording.mp3");

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

	public void CancelTranscription()
	{
		_state.StopTranscription();
	}
}
