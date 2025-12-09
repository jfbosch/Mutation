using CognitiveSupport;
using Microsoft.UI.Dispatching;
using Mutation.Ui.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Mutation.Ui.Core;

internal class AudioSessionManager : IDisposable
{
    private readonly SpeechToTextManager _speechManager;
    private readonly AudioDeviceManager _audioDeviceManager;
    private readonly TranscriptFormatter _transcriptFormatter;
    private readonly Settings _settings;
    private readonly MediaPlayer _playbackPlayer;

    private InMemoryRandomAccessStream? _playbackStream;
    private SpeechSession? _playingSession;
    private SpeechSession? _selectedSession;
    private bool _currentRecordingUsesLlmFormatting;

    public ObservableCollection<SpeechSession> SessionHistory { get; } = new();

    public SpeechSession? SelectedSession
    {
        get => _selectedSession;
        private set
        {
            if (_selectedSession != value)
            {
                _selectedSession = value;
                SelectedSessionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsPlaying => _playbackPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
    public bool IsRecording => _speechManager.Recording;
    public bool IsTranscribing => _speechManager.Transcribing;

    public event EventHandler? SelectedSessionChanged;
    public event EventHandler? PlaybackStarted;
    public event EventHandler? PlaybackStopped;
    public event EventHandler? StateChanged;
    
    public event EventHandler<string>? TranscriptReady;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? StatusMessage;

    public AudioSessionManager(
        SpeechToTextManager speechManager,
        AudioDeviceManager audioDeviceManager,
        TranscriptFormatter transcriptFormatter,
        Settings settings)
    {
        _speechManager = speechManager;
        _audioDeviceManager = audioDeviceManager;
        _transcriptFormatter = transcriptFormatter;
        _settings = settings;

        _playbackPlayer = new MediaPlayer { AutoPlay = false };
        _playbackPlayer.MediaEnded += PlaybackPlayer_MediaEnded;
        _playbackPlayer.MediaFailed += PlaybackPlayer_MediaFailed;
    }

    public void RefreshSessions(SpeechSession? preferredSelection = null, string? preferredPath = null)
    {
        var snapshot = _speechManager.GetSessions();
        SessionHistory.Clear();
        foreach (var session in snapshot)
        {
            SessionHistory.Add(session);
        }

        string? path = preferredPath;
        if (preferredSelection != null)
        {
            path = preferredSelection.FilePath;
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            SelectedSession = SessionHistory.FirstOrDefault(s => PathsEqual(s.FilePath, path));
        }
        
        if (SelectedSession == null && SessionHistory.Count > 0)
        {
            SelectedSession = SessionHistory.FirstOrDefault();
        }
        else if (SessionHistory.Count == 0)
        {
            SelectedSession = null;
        }
    }

    public void NavigateSessions(int direction)
    {
        if (IsRecording || IsTranscribing)
            return;

        RefreshSessions(preferredSelection: SelectedSession);

        if (SessionHistory.Count == 0)
            return;

        int currentIndex = SelectedSession != null ? SessionHistory.IndexOf(SelectedSession) : -1;
        if (currentIndex < 0)
            currentIndex = 0;

        int targetIndex = direction < 0 ? currentIndex - 1 : currentIndex + 1;
        if (targetIndex < 0 || targetIndex >= SessionHistory.Count)
            return;

        var targetSession = SessionHistory[targetIndex];

        StopPlayback();
        SelectedSession = targetSession;
        
        // Auto-play when navigating? The original code did:
        // await StartPlaybackAsync(targetSession);
        // We will expose a method to play and let the UI decide or handle it here.
        // The original code called StartPlaybackAsync inside NavigateSessionsAsync.
        // So we should probably trigger playback or let the caller do it.
        // For now, let's just change selection and let the caller call PlaySelectedSessionAsync if desired.
        // Wait, the original code was: await StartPlaybackAsync(targetSession);
        // So I should probably do that too, but this method is void.
        // I'll make it async or fire an event.
        // Better: The caller (UI) calls Navigate, then Play.
        // Or I can make NavigateSessionsAsync.
    }

    public async Task NavigateSessionsAsync(int direction)
    {
        if (IsRecording || IsTranscribing)
            return;

        RefreshSessions(preferredSelection: SelectedSession);

        if (SessionHistory.Count == 0)
            return;

        int currentIndex = SelectedSession != null ? SessionHistory.IndexOf(SelectedSession) : -1;
        if (currentIndex < 0)
            currentIndex = 0;

        int targetIndex = direction < 0 ? currentIndex - 1 : currentIndex + 1;
        if (targetIndex < 0 || targetIndex >= SessionHistory.Count)
            return;

        var targetSession = SessionHistory[targetIndex];

        StopPlayback();
        SelectedSession = targetSession;
        await PlaySelectedSessionAsync();
    }

    public async Task StartStopRecordingAsync(ISpeechToTextService activeService, bool useLlmFormatting, string prompt, string llmPrompt = "")
    {
        try
        {
            if (IsTranscribing)
            {
                _speechManager.CancelTranscription();
                BeepPlayer.Play(BeepType.Failure);
                StateChanged?.Invoke(this, EventArgs.Empty);
                StatusMessage?.Invoke(this, "Transcription cancelled.");
                return;
            }

            if (!IsRecording)
            {
                _currentRecordingUsesLlmFormatting = useLlmFormatting;
                StopPlayback();
                
                StatusMessage?.Invoke(this, "Listening for audio...");
                StateChanged?.Invoke(this, EventArgs.Empty); // Notify UI to update buttons (Stop)

                var session = await _speechManager.StartRecordingAsync(_audioDeviceManager.MicrophoneDeviceIndex);
                RefreshSessions(session);
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _currentRecordingUsesLlmFormatting = useLlmFormatting;
                StopPlayback();
                StatusMessage?.Invoke(this, "Transcribing your recording...");
                StateChanged?.Invoke(this, EventArgs.Empty); // Notify UI to update buttons (Transcribing...)

                try
                {
                    string text = await _speechManager.StopRecordingAndTranscribeAsync(activeService, prompt, CancellationToken.None);
                    await ProcessTranscriptAsync(text, llmPrompt);
                }
                catch (OperationCanceledException)
                {
                    StatusMessage?.Invoke(this, "Transcription cancelled.");
                }
                finally
                {
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task RetryTranscriptionAsync(ISpeechToTextService activeService, string prompt)
    {
        if (IsRecording || IsTranscribing)
        {
            StatusMessage?.Invoke(this, "Finish the current operation before retrying.");
            return;
        }

        if (SelectedSession == null)
        {
            StatusMessage?.Invoke(this, "No session available to retry.");
            return;
        }

        try
        {
            StopPlayback();
            StatusMessage?.Invoke(this, "Transcribing your recording...");
            StateChanged?.Invoke(this, EventArgs.Empty);

            string text = await _speechManager.TranscribeExistingRecordingAsync(activeService, SelectedSession, prompt, CancellationToken.None);
            // Retry usually doesn't apply LLM formatting in the original code unless explicitly requested?
            // Original code: FinalizeTranscript(text, "Transcript refreshed from the selected session.");
            // It seems it just returns the raw text.
            TranscriptReady?.Invoke(this, text);
            StatusMessage?.Invoke(this, "Transcript refreshed from the selected session.");
        }
        catch (OperationCanceledException)
        {
            StatusMessage?.Invoke(this, "Transcription cancelled.");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
        }
        finally
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task ImportAudioAsync(StorageFile file, ISpeechToTextService activeService, string prompt)
    {
        if (IsRecording || IsTranscribing)
        {
            StatusMessage?.Invoke(this, "Finish the current operation before uploading.");
            return;
        }

        try
        {
            StopPlayback();
            StatusMessage?.Invoke(this, $"Transcribing {file.Name}...");
            StateChanged?.Invoke(this, EventArgs.Empty);

            var session = await _speechManager.ImportUploadedAudioAsync(file.Path, CancellationToken.None);
            RefreshSessions(session);
            
            string text = await _speechManager.TranscribeExistingRecordingAsync(activeService, session, prompt, CancellationToken.None);
            TranscriptReady?.Invoke(this, text);
            StatusMessage?.Invoke(this, $"Transcript generated from {session.FileName}.");
        }
        catch (OperationCanceledException)
        {
            StatusMessage?.Invoke(this, "Transcription cancelled.");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
        }
        finally
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task ProcessTranscriptAsync(string text, string llmPrompt)
    {
        // Always run rules-based formatting first
        string rulesFormattedText = _transcriptFormatter.ApplyRules(text, false);
        string finalFormattedText = rulesFormattedText;

        if (_currentRecordingUsesLlmFormatting)
        {
            try
            {
                StatusMessage?.Invoke(this, "Formatting with LLM...");
                string modelName = _settings.LlmSettings.SelectedLlmModel ?? LlmSettings.DefaultModel;
                // Pass the rules-formatted text to the LLM
                finalFormattedText = await _transcriptFormatter.FormatWithLlmAsync(rulesFormattedText, llmPrompt, modelName);
            }
            catch (Exception ex)
            {
                StatusMessage?.Invoke(this, $"LLM formatting failed: {ex.Message}. Using rules-formatted transcript.");
                // finalFormattedText remains rulesFormattedText
            }
        }

        TranscriptReady?.Invoke(this, finalFormattedText);
        StatusMessage?.Invoke(this, "Transcript ready and copied.");
    }

    public async Task PlaySelectedSessionAsync()
    {
        if (SelectedSession == null) return;

        if (IsPlaying && _playingSession != null && PathsEqual(_playingSession.FilePath, SelectedSession.FilePath))
        {
            StopPlayback();
            return;
        }

        try
        {
            StopPlayback();

            if (!File.Exists(SelectedSession.FilePath))
            {
                StatusMessage?.Invoke(this, "Audio file not found.");
                RefreshSessions();
                return;
            }

            byte[] audioBytes = await File.ReadAllBytesAsync(SelectedSession.FilePath);
            _playbackStream = new InMemoryRandomAccessStream();
            using (var dataWriter = new DataWriter(_playbackStream))
            {
                dataWriter.WriteBytes(audioBytes);
                await dataWriter.StoreAsync();
                await dataWriter.FlushAsync();
                dataWriter.DetachStream();
            }
            _playbackStream.Seek(0);

            _playbackPlayer.Source = MediaSource.CreateFromStream(_playbackStream, SelectedSession.Extension);
            _playingSession = SelectedSession;
            _playbackPlayer.Play();
            
            PlaybackStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StopPlayback();
            ErrorOccurred?.Invoke(this, $"Playback failed: {ex.Message}");
        }
    }

    public void StopPlayback()
    {
        try
        {
            _playbackPlayer.Pause();
            _playbackPlayer.Source = null;
        }
        catch { }

        _playbackStream?.Dispose();
        _playbackStream = null;
        _playingSession = null;
        
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    private void PlaybackPlayer_MediaEnded(MediaPlayer sender, object args)
    {
        // Marshal to UI thread if needed? Events are usually handled on UI thread if invoked from there, 
        // but MediaEnded might be on a background thread.
        // We'll invoke the event and let the UI handler dispatch if necessary, 
        // or we can use DispatcherQueue if we had access to it.
        // Since this is a manager, it's better to just fire the event.
        StopPlayback();
    }

    private void PlaybackPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        StopPlayback();
        ErrorOccurred?.Invoke(this, $"Playback failed: {args.ErrorMessage}");
    }

    public void Dispose()
    {
        _playbackPlayer.MediaEnded -= PlaybackPlayer_MediaEnded;
        _playbackPlayer.MediaFailed -= PlaybackPlayer_MediaFailed;
        _playbackPlayer.Dispose();
        _playbackStream?.Dispose();
    }

    public Task CleanupSessionsAsync()
    {
        var exclusions = new List<string>();
        if (SelectedSession != null)
            exclusions.Add(SelectedSession.FilePath);
        if (_playingSession != null)
            exclusions.Add(_playingSession.FilePath);
        if (_speechManager.CurrentRecordingSession != null)
            exclusions.Add(_speechManager.CurrentRecordingSession.FilePath);

        return _speechManager.CleanupSessionsAsync(exclusions);
    }

    public async Task EnsureStoppedAsync()
    {
        if (IsRecording)
        {
            await _speechManager.StopRecordingAsync();
        }
        if (IsTranscribing)
        {
            _speechManager.CancelTranscription();
        }
    }

    private static bool PathsEqual(string? p1, string? p2)
    {
        if (string.IsNullOrWhiteSpace(p1) || string.IsNullOrWhiteSpace(p2))
            return false;
        return string.Equals(Path.GetFullPath(p1), Path.GetFullPath(p2), StringComparison.OrdinalIgnoreCase);
    }
}
