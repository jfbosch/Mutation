using CognitiveSupport;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Mutation.Ui.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Mutation.Ui;

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
	private readonly ITextToSpeechService _textToSpeech;
	private readonly Settings _settings;

	// Suppress auto-format/clipboard/beep when we change text programmatically or during record/transcribe
	private bool _suppressAutoActions = false;

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
		_settings = settings;
		_speechManager = new SpeechToTextManager(settings);

		InitializeComponent();
		_audioDeviceManager.EnsureDefaultMicrophoneSelected();

		BtnToggleMic.Content = _audioDeviceManager.IsMuted ? "Unmute" : "Mute";
		var micList = _audioDeviceManager.CaptureDevices.ToList();
		CmbMicrophone.ItemsSource = micList;
		CmbMicrophone.DisplayMemberPath = nameof(CoreAudio.MMDevice.FriendlyName);

		RestorePersistedMicrophoneSelection(micList);

		CmbSpeechService.ItemsSource = _speechServices;
		CmbSpeechService.DisplayMemberPath = nameof(ISpeechToTextService.ServiceName);

		RestorePersistedSpeechServiceSelection();

		TxtFormatPrompt.Text = _settings.LlmSettings?.FormatTranscriptPrompt ?? string.Empty;

		var tooltipManager = new TooltipManager(_settings);
		tooltipManager.SetupTooltips(TxtSpeechToText, TxtFormatTranscript);

		CmbInsertOption.ItemsSource = Enum.GetValues(typeof(DictationInsertOption)).Cast<DictationInsertOption>().ToList();
		CmbInsertOption.SelectedItem = DictationInsertOption.Paste;

		this.Closed += MainWindow_Closed;
	}

	private void RestorePersistedSpeechServiceSelection()
	{
		string? savedServiceName = _settings.SpeetchToTextSettings?.ActiveSpeetchToTextService;
		if (!string.IsNullOrWhiteSpace(savedServiceName))
		{
			var match = _speechServices.FirstOrDefault(s => s.ServiceName == savedServiceName);
			if (match != null)
			{
				CmbSpeechService.SelectedItem = match;
				_activeSpeechService = match;
			}
			else if (_speechServices.Length > 0)
			{
				CmbSpeechService.SelectedIndex = 0;
				_activeSpeechService = _speechServices[0];
			}
		}
		else if (_speechServices.Length > 0)
		{
			CmbSpeechService.SelectedIndex = 0;
			_activeSpeechService = _speechServices[0];
		}
	}

	private void RestorePersistedMicrophoneSelection(System.Collections.Generic.List<CoreAudio.MMDevice> micList)
	{
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
	}

	private async void MainWindow_Closed(object sender, WindowEventArgs args)
	{
		// Prevent auto actions during shutdown
		_suppressAutoActions = true;
		try
		{
			// Ensure we are not recording/transcribing when closing
			if (_speechManager.Recording)
			{
				await _speechManager.StopRecordingAsync();
			}
			if (_speechManager.Transcribing)
			{
				_speechManager.CancelTranscription();
			}
		}
		catch { }
		_uiStateManager.Save(this);

		if (_activeSpeechService != null)
			_settings.SpeetchToTextSettings!.ActiveSpeetchToTextService = _activeSpeechService.ServiceName;
		_settings.LlmSettings!.FormatTranscriptPrompt = TxtFormatPrompt.Text;

		_settingsManager.SaveSettingsToFile(_settings);
		BeepPlayer.DisposePlayers();
	}

	private void CopyText_Click(object sender, RoutedEventArgs e)
	{
		_clipboard.SetText(TxtClipboard.Text);
	}

	public void BtnToggleMic_Click(object? sender, RoutedEventArgs? e)
	{
		_audioDeviceManager.ToggleMute();
		BtnToggleMic.Content = _audioDeviceManager.IsMuted ? "Unmute" : "Mute";
		BeepPlayer.Play(_audioDeviceManager.IsMuted ? BeepType.Mute : BeepType.Unmute);
	}

	private async void BtnScreenshot_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			await _ocrManager.TakeScreenshotToClipboardAsync();
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("Screenshot Error", ex);
		}
	}

	private async void BtnScreenshotOcr_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			var result = await _ocrManager.TakeScreenshotAndExtractTextAsync(OcrReadingOrder.TopToBottomColumnAware);
			SetOcrText(result.Message);
			HotkeyManager.SendHotkeyAfterDelay(_settings.AzureComputerVisionSettings?.SendKotKeyAfterOcrOperation, result.Success ? Constants.SendHotkeyDelay : Constants.FailureSendHotkeyDelay);
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("Screenshot + OCR Error", ex);
		}
	}

	private async void BtnOcrClipboard_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			var result = await _ocrManager.ExtractTextFromClipboardImageAsync(OcrReadingOrder.TopToBottomColumnAware);
			SetOcrText(result.Message);
			HotkeyManager.SendHotkeyAfterDelay(_settings.AzureComputerVisionSettings?.SendKotKeyAfterOcrOperation ?? string.Empty, result.Success ? Constants.SendHotkeyDelay : Constants.FailureSendHotkeyDelay);
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("OCR Clipboard Error", ex);
		}
	}

	public async void BtnSpeechToText_Click(object? sender, RoutedEventArgs? e)
	{
		try
		{
			await StartStopSpeechToTextAsync();
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("Speech to Text Error", ex);
		}
	}

	public async Task StartStopSpeechToTextAsync()
	{
		try
		{
			// If a transcription is currently in-flight, cancel it and play failure/cancel sound.
			if (_speechManager.Transcribing)
			{
				_speechManager.CancelTranscription();
				// Restore UI to idle state
				BtnSpeechToText.Content = "Record";
				BtnSpeechToText.IsEnabled = true;
				TxtSpeechToText.IsReadOnly = false;
				_suppressAutoActions = false;
				BeepPlayer.Play(BeepType.Failure);
				return;
			}

			if (_activeSpeechService == null)
			{
				var dlg = new ContentDialog
				{
					Title = "Warning",
					Content = new TextBlock { Text = "No speech-to-text service selected.", TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
					CloseButtonText = "OK",
					XamlRoot = this.Content.XamlRoot
				};
				Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(dlg, "Warning");
				Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(dlg, "No speech-to-text service selected.");
				await dlg.ShowAsync();
				return;
			}

			if (!_speechManager.Recording)
			{
				// Enter recording state without triggering TextChanged side-effects
				_suppressAutoActions = true;
				TxtSpeechToText.IsReadOnly = true;
				TxtSpeechToText.Text = "Recording...";
				BtnSpeechToText.Content = "Stop";
				BeepPlayer.Play(BeepType.Start);
				await _speechManager.StartRecordingAsync(_audioDeviceManager.MicrophoneDeviceIndex);
				_suppressAutoActions = false;
			}
			else
			{
				BtnSpeechToText.IsEnabled = false;
				// Show transcribing status without triggering TextChanged side-effects
				_suppressAutoActions = true;
				TxtSpeechToText.Text = "Transcribing...";
				try
				{
					string text = await _speechManager.StopRecordingAndTranscribeAsync(_activeSpeechService, string.Empty, CancellationToken.None);
					// Compute formatted transcript immediately after transcription completes.
					string formatted = _transcriptFormatter.ApplyRules(text, false);

					// Keep auto-actions suppressed while updating UI and interacting with clipboard/target app
					// to avoid triggering the debounced TextChanged handler.
					// Show raw in the main transcript box and formatted in the formatted output box.
					TxtSpeechToText.Text = text;
					TxtFormatTranscript.Text = formatted;

					BtnSpeechToText.Content = "Record";
					BtnSpeechToText.IsEnabled = true;

					// Use formatted text for clipboard and insertion into the active application.
					_clipboard.SetText(formatted);
					InsertIntoActiveApplication(formatted);

					BeepPlayer.Play(BeepType.Success);
					TxtSpeechToText.IsReadOnly = false;
					_suppressAutoActions = false;
					HotkeyManager.SendHotkeyAfterDelay(_settings.SpeetchToTextSettings?.SendKotKeyAfterTranscriptionOperation, Constants.SendHotkeyDelay);
				}
				catch (OperationCanceledException)
				{
					// Graceful cancel: ensure UI is reset; failure beep already played at cancel trigger.
					BtnSpeechToText.Content = "Record";
					BtnSpeechToText.IsEnabled = true;
					TxtSpeechToText.IsReadOnly = false;
					_suppressAutoActions = false;
					return;
				}
			}
		}
		catch (Exception ex)
		{
			await ShowErrorDialog("Speech to Text Error", ex);
		}
	}

	private async void ShowMessage(string title, string message)
	{
		var dialog = new ContentDialog
		{
			Title = title,
			Content = new TextBlock { Text = message, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
			CloseButtonText = "OK",
			XamlRoot = this.Content.XamlRoot // important in WinUI 3
		};
		Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(dialog, title);
		Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(dialog, message);

		await dialog.ShowAsync();
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
		catch (Exception ex)
		{
			await ShowErrorDialog("Format with LLM Error", ex);
		}
	}



	// Apply review issues functionality removed

	public async Task ShowErrorDialog(string title, Exception ex)
	{
		string message = $"An error occurred:\n{ex.Message}\n\n{ex}";
		var dialog = new ContentDialog
		{
			Title = title,
			Content = new TextBlock { Text = message, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
			CloseButtonText = "OK",
			XamlRoot = (this.Content as FrameworkElement)?.XamlRoot
		};
		Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(dialog, title);
		Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(dialog, message);
		await dialog.ShowAsync();
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
				//BeepPlayer.Play(BeepType.Start);
				HotkeyManager.SendHotkey("^v");
				break;
		}
	}

	private async void TxtSpeechToText_TextChanged(object sender, TextChangedEventArgs e)
	{
		// Avoid auto actions during programmatic updates or while recording/transcribing
		if (_suppressAutoActions || TxtSpeechToText.IsReadOnly || _speechManager.Recording || _speechManager.Transcribing)
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
				// Update UI preview only; do not modify clipboard or insert into active app here.
				TxtFormatTranscript.Text = formatted;
				// Intentionally do not call _clipboard.SetText or InsertIntoActiveApplication here.
				// Insertion/clipboard updates happen on transcription completion to avoid duplicates.
			}
		}
		catch (TaskCanceledException) { }
	}

	internal void SetOcrText(string message)
	{
		TxtOcr.Text = message;
	}

	// ReviewItem removed
}
