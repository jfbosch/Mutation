using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using CoreAudio;
using System.Collections.Generic;
using CognitiveSupport;
using Mutation.Ui.Services;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Mutation.Ui
{
	/// <summary>
	/// An empty window that can be used on its own or navigated to within a Frame.
	/// </summary>
        public sealed partial class MainWindow : Window
        {
                private readonly ClipboardManager _clipboard;
                private readonly UiStateManager _uiStateManager;
                private readonly ISettingsManager _settingsManager;
                private readonly AudioDeviceManager _audioDeviceManager;
                private readonly OcrManager _ocrManager;
                private readonly ISpeechToTextService[] _speechServices;
                private readonly SpeechToTextManager _speechManager;
                private readonly TranscriptFormatter _transcriptFormatter;
                private readonly TranscriptReviewer _transcriptReviewer;
        private readonly ITextToSpeechService _textToSpeech;
        private readonly Settings _settings;

        private ISpeechToTextService? _activeSpeechService;
        private CancellationTokenSource _formatDebounceCts = new();
        private DictationInsertOption _insertOption = DictationInsertOption.Paste;

                public MainWindow(
                        ClipboardManager clipboard,
                        UiStateManager uiStateManager,
                        AudioDeviceManager audioDeviceManager,
                        OcrManager ocrManager,
                        ISpeechToTextService[] speechServices,
                        ITextToSpeechService textToSpeech,
                        TranscriptFormatter transcriptFormatter,
                        TranscriptReviewer transcriptReviewer,
                        ISettingsManager settingsManager,
                        Settings settings)
                {
                        _clipboard = clipboard;
                        _uiStateManager = uiStateManager;
                        _settingsManager = settingsManager;
                        _audioDeviceManager = audioDeviceManager;
                        _ocrManager = ocrManager;
                        _speechServices = speechServices;
                        _textToSpeech = textToSpeech;
                        _transcriptFormatter = transcriptFormatter;
                        _transcriptReviewer = transcriptReviewer;
                        _settings = settings;
                        _speechManager = new SpeechToTextManager(settings);

                        // Ensure a default microphone is selected
                        _audioDeviceManager.EnsureDefaultMicrophoneSelected();

                        InitializeComponent();
                        TxtMicState.Text = _audioDeviceManager.IsMuted ? "Muted" : "Unmuted";
                        var micList = _audioDeviceManager.CaptureDevices.ToList();
                        CmbMicrophone.ItemsSource = micList;
                        CmbMicrophone.DisplayMemberPath = nameof(CoreAudio.MMDevice.FriendlyName);

                        // Restore persisted microphone selection
                        string? savedMicFullName = _settings.AudioSettings?.ActiveCaptureDeviceFullName;
                        if (!string.IsNullOrWhiteSpace(savedMicFullName))
                        {
                            var match = micList.FirstOrDefault(m => m.FriendlyName == savedMicFullName);
                            if (match != null)
                                CmbMicrophone.SelectedItem = match;
                            else if (_audioDeviceManager.Microphone != null)
                                CmbMicrophone.SelectedItem = _audioDeviceManager.Microphone;
                            else if (micList.Count > 0)
                                CmbMicrophone.SelectedIndex = 0;
                        }
                        else if (_audioDeviceManager.Microphone != null)
                                CmbMicrophone.SelectedItem = _audioDeviceManager.Microphone;
                        else if (micList.Count > 0)
                                CmbMicrophone.SelectedIndex = 0;

                        CmbSpeechService.ItemsSource = _speechServices;
                        CmbSpeechService.DisplayMemberPath = nameof(ISpeechToTextService.ServiceName);
                        if (_activeSpeechService != null)
                                CmbSpeechService.SelectedItem = _activeSpeechService;

                        TxtFormatPrompt.Text = _settings.LlmSettings?.FormatTranscriptPrompt ?? string.Empty;
                        TxtReviewPrompt.Text = _settings.LlmSettings?.ReviewTranscriptPrompt ?? string.Empty;

                        var tooltipManager = new TooltipManager(_settings);
                        tooltipManager.SetupTooltips(TxtSpeechToText, TxtFormatTranscript);

                        CmbInsertOption.ItemsSource = Enum.GetValues(typeof(DictationInsertOption)).Cast<DictationInsertOption>().ToList();
                        CmbInsertOption.SelectedItem = DictationInsertOption.Paste;

                        this.Closed += MainWindow_Closed;
                }

        private async Task ShowErrorDialogAsync(string message, string title = "Error")
        {
            var errorDialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }

                private async void MainWindow_Closed(object sender, WindowEventArgs args)
                {
                        _uiStateManager.Save(this);

                        if (_activeSpeechService != null && _settings.SpeetchToTextSettings != null) // Null check for safety
                                _settings.SpeetchToTextSettings.ActiveSpeetchToTextService = _activeSpeechService.ServiceName;
                        if (_settings.LlmSettings != null) // Null check for safety
                        {
                            _settings.LlmSettings.FormatTranscriptPrompt = TxtFormatPrompt.Text;
                            _settings.LlmSettings.ReviewTranscriptPrompt = TxtReviewPrompt.Text;
                        }

                        _settingsManager.SaveSettingsToFile(_settings);
                        BeepPlayer.DisposePlayers();
                }

                private void BtnBeep_Click(object sender, RoutedEventArgs e)
                {
                        BeepPlayer.Play(BeepType.Success);
                }

                private void CopyText_Click(object sender, RoutedEventArgs e)
                {
                        _clipboard.SetText(TxtClipboard.Text);
                }

        public void BtnToggleMic_Click(object? sender, RoutedEventArgs? e)
        {
                _audioDeviceManager.ToggleMute();
                TxtMicState.Text = _audioDeviceManager.IsMuted ? "Muted" : "Unmuted";
                BeepPlayer.Play(_audioDeviceManager.IsMuted ? BeepType.Mute : BeepType.Unmute);
        }

                private async void BtnScreenshot_Click(object sender, RoutedEventArgs e)
                {
                        await _ocrManager.TakeScreenshotToClipboardAsync();
                        HotkeyManager.SendHotkeyAfterDelay(_settings.AzureComputerVisionSettings?.SendKotKeyAfterOcrOperation ?? string.Empty, 50);
                }

                private async void BtnScreenshotOcr_Click(object sender, RoutedEventArgs e)
                {
                        var result = await _ocrManager.TakeScreenshotAndExtractTextAsync(OcrReadingOrder.TopToBottomColumnAware);
                        TxtOcr.Text = result.Message;
                        HotkeyManager.SendHotkeyAfterDelay(_settings.AzureComputerVisionSettings?.SendKotKeyAfterOcrOperation ?? string.Empty, result.Success ? 50 : 25);
                }

                private async void BtnOcrClipboard_Click(object sender, RoutedEventArgs e)
                {
                        var result = await _ocrManager.ExtractTextFromClipboardImageAsync(OcrReadingOrder.TopToBottomColumnAware);
                        TxtOcr.Text = result.Message;
                        HotkeyManager.SendHotkeyAfterDelay(_settings.AzureComputerVisionSettings?.SendKotKeyAfterOcrOperation ?? string.Empty, result.Success ? 50 : 25);
                }

                public async void BtnSpeechToText_Click(object? sender, RoutedEventArgs? e)
                {
                        await StartStopSpeechToTextAsync();
                }

                public async Task StartStopSpeechToTextAsync()
                {
                        if (_activeSpeechService == null)
                        {
                                // Using the new helper for consistency, though this is a warning.
                                await ShowErrorDialogAsync("No speech-to-text service selected.", "Warning");
                                return;
                        }

                        bool wasRecording = _speechManager.Recording;
                        try
                        {
                                if (!wasRecording)
                                {
                                        TxtSpeechToText.Text = "Recording...";
                                        BtnSpeechToText.Content = "Stop";
                                        BeepPlayer.Play(BeepType.Start);
                                        await _speechManager.StartRecordingAsync(_audioDeviceManager.MicrophoneDeviceIndex);
                                }
                                else
                                {
                                        BtnSpeechToText.IsEnabled = false;
                                        TxtSpeechToText.Text = "Transcribing...";
                                        // Ensure _activeSpeechService is not null again, though checked above.
                                        // This is more for static analysis or extreme edge cases.
                                        if (_activeSpeechService == null) 
                                        {
                                            await ShowErrorDialogAsync("Speech service became unselected during operation.", "Error");
                                            return; // Early exit
                                        }
                                        string text = await _speechManager.StopRecordingAndTranscribeAsync(_activeSpeechService, string.Empty, CancellationToken.None);
                                        BeepPlayer.Play(BeepType.End);
                                        TxtSpeechToText.Text = text;
                                        _clipboard.SetText(text);
                                        InsertIntoActiveApplication(text);
                                        BeepPlayer.Play(BeepType.Success);
                                        HotkeyManager.SendHotkeyAfterDelay(_settings.SpeetchToTextSettings?.SendKotKeyAfterTranscriptionOperation ?? string.Empty, 50);
                                }
                        }
                        catch (Exception ex)
                        {
                                BeepPlayer.Play(BeepType.Failure); // Play failure beep on error
                                await ShowErrorDialogAsync(ex.Message, "Speech-to-Text Error");
                                // If an error occurred while trying to stop, text might still be "Transcribing..."
                                // Or if error during start, it might be "Recording..."
                                // Resetting to a neutral state is important.
                                if (wasRecording) // Error likely during StopRecordingAndTranscribeAsync
                                {
                                   TxtSpeechToText.Text = "Error during transcription. Please try again.";
                                }
                                else // Error likely during StartRecordingAsync
                                {
                                   TxtSpeechToText.Text = "Error starting recording. Please try again.";
                                }
                        }
                        finally
                        {
                                // Always reset UI to a consistent state
                                BtnSpeechToText.Content = "Record";
                                BtnSpeechToText.IsEnabled = true;
                                // If _speechManager.Recording is still true here, it means StartRecordingAsync failed
                                // or StopRecordingAndTranscribeAsync failed before stopping the underlying recorder.
                                // A more robust _speechManager would ensure its state is consistent.
                                // For now, we assume the UI reset is the primary goal.
                                if (_speechManager.Recording && wasRecording)
                                {
                                    // If it was recording and still is, means stop failed before transcription step.
                                    // Attempt to gracefully stop the underlying recording if possible,
                                    // though _speechManager should ideally handle this.
                                    // For now, we focus on UI consistency.
                                }
                        }
                }

                public void BtnTextToSpeech_Click(object? sender, RoutedEventArgs? e)
                {
                        string text = _clipboard.GetText();
                        _textToSpeech.SpeakText(text);
                }

        public void BtnFormatTranscript_Click(object? sender, RoutedEventArgs? e)
        {
                string raw = TxtSpeechToText.Text;
                string formatted = _transcriptFormatter.ApplyRules(raw, false);
                TxtFormatTranscript.Text = formatted;
                _clipboard.SetText(formatted);
                InsertIntoActiveApplication(formatted);
                BeepPlayer.Play(BeepType.Success);
        }

        public async void BtnFormatLlm_Click(object? sender, RoutedEventArgs? e)
        {
                string originalText = TxtFormatTranscript.Text; // Save original in case of error
                try
                {
                        TxtFormatTranscript.Text = "Formatting...";
                        string raw = TxtSpeechToText.Text;
                        string prompt = TxtFormatPrompt.Text;
                        string formatted = await _transcriptFormatter.FormatWithLlmAsync(raw, prompt);
                        TxtFormatTranscript.Text = formatted;
                        _clipboard.SetText(formatted);
                        InsertIntoActiveApplication(formatted);
                        BeepPlayer.Play(BeepType.Success);
                }
                catch (LlmServiceException ex)
                {
                        BeepPlayer.Play(BeepType.Failure);
                        await ShowErrorDialogAsync(ex.Message, "LLM Formatting Error");
                        TxtFormatTranscript.Text = originalText; // Reset on error
                }
                catch (Exception ex)
                {
                        BeepPlayer.Play(BeepType.Failure);
                        await ShowErrorDialogAsync(ex.Message, "Formatting Error");
                        TxtFormatTranscript.Text = originalText; // Reset on error
                }
        }

        public async void BtnReviewTranscript_Click(object? sender, RoutedEventArgs? e)
        {
                try
                {
                        string transcript = TxtFormatTranscript.Text;
                        string prompt = TxtReviewPrompt.Text;
                        // Potentially indicate work in progress if review is slow
                        // TxtReviewTranscript.Text = "Reviewing..."; 
                        string review = await _transcriptReviewer.ReviewAsync(transcript, prompt, 0.4m);
                        TxtReviewTranscript.Text = review;
                        var issues = review.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(i => new ReviewItem { Apply = false, Issue = i })
                                             .ToList();
                        GridReview.ItemsSource = issues;
                        BeepPlayer.Play(BeepType.Success); // Play success after review
                }
                catch (LlmServiceException ex)
                {
                        BeepPlayer.Play(BeepType.Failure);
                        await ShowErrorDialogAsync(ex.Message, "LLM Review Error");
                        TxtReviewTranscript.Text = "Error during review."; // Clear or indicate error
                }
                catch (Exception ex)
                {
                        BeepPlayer.Play(BeepType.Failure);
                        await ShowErrorDialogAsync(ex.Message, "Review Error");
                        TxtReviewTranscript.Text = "Error during review."; // Clear or indicate error
                }
        }

        private async void BtnApplySelectedReviewIssues_Click(object sender, RoutedEventArgs e)
        {
                if (!(GridReview.ItemsSource is IEnumerable<ReviewItem> items)) return;

                var selected = items.Where(i => i.Apply).Select(i => i.Issue).ToArray();
                if (selected.Length == 0) return;

                string originalTranscript = TxtFormatTranscript.Text;
                TxtFormatTranscript.IsReadOnly = true;
                try
                {
                        // Optionally indicate work: TxtFormatTranscript.Text = "Applying corrections...";
                        string prompt = TxtReviewPrompt.Text;
                        string revision = await _transcriptReviewer.ApplyCorrectionsAsync(originalTranscript, prompt, selected);
                        TxtFormatTranscript.Text = revision;
                        GridReview.ItemsSource = items.Where(i => !i.Apply).ToList(); // Update remaining issues
                        BeepPlayer.Play(BeepType.Success);
                }
                catch (LlmServiceException ex)
                {
                        BeepPlayer.Play(BeepType.Failure);
                        await ShowErrorDialogAsync(ex.Message, "LLM Apply Corrections Error");
                        TxtFormatTranscript.Text = originalTranscript; // Revert on error
                }
                catch (Exception ex)
                {
                        BeepPlayer.Play(BeepType.Failure);
                        await ShowErrorDialogAsync(ex.Message, "Apply Corrections Error");
                        TxtFormatTranscript.Text = originalTranscript; // Revert on error
                }
                finally
                {
                        TxtFormatTranscript.IsReadOnly = false;
                }
        }

        private void CmbMicrophone_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
                if (CmbMicrophone.SelectedItem is CoreAudio.MMDevice device)
                {
                        _audioDeviceManager.SelectMicrophone(device);
                        if (_settings.AudioSettings != null)
                        {
                                _settings.AudioSettings.ActiveCaptureDeviceFullName = device.FriendlyName;
                                _settingsManager.SaveSettingsToFile(_settings);
                        }
                }
        }

        private void CmbSpeechService_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
                if (CmbSpeechService.SelectedItem is ISpeechToTextService svc)
                        _activeSpeechService = svc;
        }

        private void CmbInsertOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
                if (CmbInsertOption.SelectedItem is DictationInsertOption opt)
                        _insertOption = opt;
        }

        private void InsertIntoActiveApplication(string text)
        {
                if (string.IsNullOrWhiteSpace(text))
                        return;

                switch (_insertOption)
                {
                        case DictationInsertOption.SendKeys:
                                BeepPlayer.Play(BeepType.Start);
                                HotkeyManager.SendText(text);
                                break;
                        case DictationInsertOption.Paste:
                                _clipboard.SetText(text);
                                BeepPlayer.Play(BeepType.Start);
                                HotkeyManager.SendHotkey("CTRL+V");
                                break;
                }
        }

        private async void TxtSpeechToText_TextChanged(object sender, TextChangedEventArgs e)
        {
                if (TxtSpeechToText.IsReadOnly)
                        return;

                _formatDebounceCts.Cancel();
                _formatDebounceCts = new CancellationTokenSource();
                var token = _formatDebounceCts.Token;
                try
                {
                        await Task.Delay(300, token);
                        if (!token.IsCancellationRequested)
                        {
                                string raw = TxtSpeechToText.Text;
                                string formatted = _transcriptFormatter.ApplyRules(raw, false);
                                TxtFormatTranscript.Text = formatted;
                                _clipboard.SetText(formatted);
                                InsertIntoActiveApplication(formatted);
                                BeepPlayer.Play(BeepType.Success);
                        }
                }
                catch (TaskCanceledException) { }
        }

        private class ReviewItem
        {
                public bool Apply { get; set; }
                public string Issue { get; set; } = string.Empty;
        }
        }
}
