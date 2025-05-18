using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
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
                        _activeSpeechService = _speechServices.FirstOrDefault();
                        InitializeComponent();
                        TxtMicState.Text = _audioDeviceManager.IsMuted ? "Muted" : "Unmuted";
                        CmbMicrophone.ItemsSource = _audioDeviceManager.CaptureDevices.ToList();
                        CmbMicrophone.DisplayMemberPath = nameof(AudioSwitcher.AudioApi.CoreAudio.CoreAudioDevice.FullName);
                        if (_audioDeviceManager.Microphone != null)
                                CmbMicrophone.SelectedItem = _audioDeviceManager.Microphone;

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

                private async void MainWindow_Closed(object sender, WindowEventArgs args)
                {
                        _uiStateManager.Save(this);

                        if (_activeSpeechService != null)
                                _settings.SpeetchToTextSettings!.ActiveSpeetchToTextService = _activeSpeechService.ServiceName;
                        _settings.LlmSettings!.FormatTranscriptPrompt = TxtFormatPrompt.Text;
                        _settings.LlmSettings!.ReviewTranscriptPrompt = TxtReviewPrompt.Text;

                        await _settingsManager.SaveAsync(_settings);
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
                                var dlg = new ContentDialog
                                {
                                        Title = "Warning",
                                        Content = "No speech-to-text service selected.",
                                        CloseButtonText = "OK",
                                        XamlRoot = this.Content.XamlRoot
                                };
                                await dlg.ShowAsync();
                                return;
                        }

                        if (!_speechManager.Recording)
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
                                string text = await _speechManager.StopRecordingAndTranscribeAsync(_activeSpeechService, string.Empty, CancellationToken.None);
                                BeepPlayer.Play(BeepType.End);
                                TxtSpeechToText.Text = text;
                                BtnSpeechToText.Content = "Record";
                                BtnSpeechToText.IsEnabled = true;
                                _clipboard.SetText(text);
                                InsertIntoActiveApplication(text);
                                BeepPlayer.Play(BeepType.Success);
                                HotkeyManager.SendHotkeyAfterDelay(_settings.SpeetchToTextSettings?.SendKotKeyAfterTranscriptionOperation ?? string.Empty, 50);
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
                TxtFormatTranscript.Text = "Formatting...";
                string raw = TxtSpeechToText.Text;
                string prompt = TxtFormatPrompt.Text;
                string formatted = await _transcriptFormatter.FormatWithLlmAsync(raw, prompt);
                TxtFormatTranscript.Text = formatted;
                _clipboard.SetText(formatted);
                InsertIntoActiveApplication(formatted);
                BeepPlayer.Play(BeepType.Success);
        }

        public async void BtnReviewTranscript_Click(object? sender, RoutedEventArgs? e)
        {
                string transcript = TxtFormatTranscript.Text;
                string prompt = TxtReviewPrompt.Text;
                string review = await _transcriptReviewer.ReviewAsync(transcript, prompt, 0.4m);
                TxtReviewTranscript.Text = review;
                var issues = review.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(i => new ReviewItem { Apply = false, Issue = i })
                                     .ToList();
                GridReview.ItemsSource = issues;
        }

        private async void BtnApplySelectedReviewIssues_Click(object sender, RoutedEventArgs e)
        {
                if (GridReview.ItemsSource is IEnumerable<ReviewItem> items)
                {
                        var selected = items.Where(i => i.Apply).Select(i => i.Issue).ToArray();
                        if (selected.Length > 0)
                        {
                                TxtFormatTranscript.IsReadOnly = true;
                                string transcript = TxtFormatTranscript.Text;
                                string prompt = TxtReviewPrompt.Text;
                                string revision = await _transcriptReviewer.ApplyCorrectionsAsync(transcript, prompt, selected);
                                TxtFormatTranscript.Text = revision;
                                TxtFormatTranscript.IsReadOnly = false;
                                GridReview.ItemsSource = items.Where(i => !i.Apply).ToList();
                                BeepPlayer.Play(BeepType.Success);
                        }
                }
        }

        private void CmbMicrophone_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
                if (CmbMicrophone.SelectedItem is AudioSwitcher.AudioApi.CoreAudio.CoreAudioDevice device)
                {
                        _audioDeviceManager.SelectMicrophone(device);
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
                if (string.IsNullOrWhiteSpace(text) || this.IsActive)
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
