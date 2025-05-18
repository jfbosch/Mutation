using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
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
                private readonly AudioDeviceManager _audioDeviceManager;
                private readonly OcrManager _ocrManager;
                private readonly ISpeechToTextService[] _speechServices;
                private readonly SpeechToTextManager _speechManager;
                private readonly TranscriptFormatter _transcriptFormatter;
                private readonly TranscriptReviewer _transcriptReviewer;
                private readonly ITextToSpeechService _textToSpeech;
                private readonly Settings _settings;

                private ISpeechToTextService? _activeSpeechService;

                public MainWindow(
                        ClipboardManager clipboard,
                        UiStateManager uiStateManager,
                        AudioDeviceManager audioDeviceManager,
                        OcrManager ocrManager,
                        ISpeechToTextService[] speechServices,
                        ITextToSpeechService textToSpeech,
                        TranscriptFormatter transcriptFormatter,
                        TranscriptReviewer transcriptReviewer,
                        Settings settings)
                {
                        _clipboard = clipboard;
                        _uiStateManager = uiStateManager;
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
                        this.Closed += MainWindow_Closed;
                }

                private void MainWindow_Closed(object sender, WindowEventArgs args)
                {
                        _uiStateManager.Save(this);
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
                                return;

                        if (!_speechManager.Recording)
                        {
                                TxtSpeechToText.Text = "Recording...";
                                BtnSpeechToText.Content = "Stop";
                                await _speechManager.StartRecordingAsync(_audioDeviceManager.MicrophoneDeviceIndex);
                        }
                        else
                        {
                                BtnSpeechToText.IsEnabled = false;
                                TxtSpeechToText.Text = "Transcribing...";
                                string text = await _speechManager.StopRecordingAndTranscribeAsync(_activeSpeechService, string.Empty, CancellationToken.None);
                                TxtSpeechToText.Text = text;
                                BtnSpeechToText.Content = "Record";
                                BtnSpeechToText.IsEnabled = true;
                                _clipboard.SetText(text);
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
                }

                public async void BtnReviewTranscript_Click(object? sender, RoutedEventArgs? e)
                {
                        string transcript = TxtFormatTranscript.Text;
                        string prompt = TxtReviewPrompt.Text;
                        string review = await _transcriptReviewer.ReviewAsync(transcript, prompt, 0.4m);
                        TxtReviewTranscript.Text = review;
                }
        }
}
