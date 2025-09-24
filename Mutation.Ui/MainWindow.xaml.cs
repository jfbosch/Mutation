using CognitiveSupport;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Mutation.Ui.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace Mutation.Ui;

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
	private readonly DispatcherTimer _statusDismissTimer;

	private const string MicOnGlyph = "\uE720";
	private const string MicOffGlyph = "\uE721";
	private const string RecordGlyph = "\uE768";
	private const string StopGlyph = "\uE71A";
	private const string ProcessingGlyph = "\uE8A0";

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

		_statusDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
		_statusDismissTimer.Tick += StatusDismissTimer_Tick;
		StatusInfoBar.CloseButtonClick += StatusInfoBar_CloseButtonClick;

		_audioDeviceManager.EnsureDefaultMicrophoneSelected();

		UpdateMicrophoneToggleVisuals();
		UpdateSpeechButtonVisuals("Start recording", RecordGlyph);
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

		// After initializing and restoring the active microphone, play a sound
		// representing the current state (mute/unmute) to reflect actual status.
		if (_audioDeviceManager.Microphone != null)
			BeepPlayer.Play(_audioDeviceManager.IsMuted ? BeepType.Mute : BeepType.Unmute);

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
		ShowStatus("Clipboard", "Text copied to the clipboard.", InfoBarSeverity.Success);
	}

	public void BtnToggleMic_Click(object? sender, RoutedEventArgs? e)
	{
		_audioDeviceManager.ToggleMute();
		UpdateMicrophoneToggleVisuals();
		ShowStatus("Microphone", _audioDeviceManager.IsMuted ? "Microphone muted." : "Microphone is live.",
			_audioDeviceManager.IsMuted ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
		BeepPlayer.Play(_audioDeviceManager.IsMuted ? BeepType.Mute : BeepType.Unmute);
	}

	private async void BtnScreenshot_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			await _ocrManager.TakeScreenshotToClipboardAsync();
			ShowStatus("Screenshot", "Screenshot copied to the clipboard.", InfoBarSeverity.Success);
		}
		catch (Exception ex)
		{
			ShowStatus("Screenshot", ex.Message, InfoBarSeverity.Error);
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
			if (result.Success)
				ShowStatus("Screenshot & OCR", "Text captured from screenshot.", InfoBarSeverity.Success);
			else
				ShowStatus("Screenshot & OCR", result.Message, InfoBarSeverity.Error);
		}
		catch (Exception ex)
		{
			ShowStatus("Screenshot & OCR", ex.Message, InfoBarSeverity.Error);
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
			if (result.Success)
				ShowStatus("OCR", "Clipboard image converted to text.", InfoBarSeverity.Success);
			else
				ShowStatus("OCR", result.Message, InfoBarSeverity.Warning);
		}
		catch (Exception ex)
		{
			ShowStatus("OCR", ex.Message, InfoBarSeverity.Error);
			await ShowErrorDialog("OCR Clipboard Error", ex);
		}
	}

	public async void BtnSpeechToText_Click(object? sender, RoutedEventArgs? e)
	{
		await StartStopSpeechToTextAsync();
	}

	public async Task StartStopSpeechToTextAsync()
	{
		try
		{
			if (_speechManager.Transcribing)
			{
				_speechManager.CancelTranscription();
				UpdateSpeechButtonVisuals("Start recording", RecordGlyph);
				BtnSpeechToText.IsEnabled = true;
				TxtSpeechToText.IsReadOnly = false;
				_suppressAutoActions = false;
				ShowStatus("Speech to Text", "Transcription cancelled.", InfoBarSeverity.Warning);
				BeepPlayer.Play(BeepType.Failure);
				return;
			}

			if (_activeSpeechService == null)
			{
				var dlg = new ContentDialog
				{
					Title = "Warning",
					Content = new TextBlock { Text = "No speech-to-text service selected.", TextWrapping = TextWrapping.Wrap },
					CloseButtonText = "OK",
					XamlRoot = this.Content.XamlRoot
				};
				AutomationProperties.SetName(dlg, "Warning");
				AutomationProperties.SetHelpText(dlg, "No speech-to-text service selected.");
				ShowStatus("Speech to Text", "Select a speech-to-text service to begin.", InfoBarSeverity.Warning);
				await dlg.ShowAsync();
				return;
			}

			if (!_speechManager.Recording)
			{
				_suppressAutoActions = true;
				TxtSpeechToText.IsReadOnly = true;
				TxtSpeechToText.Text = "Recording...";
				UpdateSpeechButtonVisuals("Stop recording", StopGlyph);
				ShowStatus("Speech to Text", "Listening for audio...", InfoBarSeverity.Informational);
				BeepPlayer.Play(BeepType.Start);
				await _speechManager.StartRecordingAsync(_audioDeviceManager.MicrophoneDeviceIndex);
				_suppressAutoActions = false;
			}
			else
			{
				BtnSpeechToText.IsEnabled = false;
				_suppressAutoActions = true;
				TxtSpeechToText.Text = "Transcribing...";
				UpdateSpeechButtonVisuals("Transcribing...", ProcessingGlyph, false);
				ShowStatus("Speech to Text", "Transcribing your recording...", InfoBarSeverity.Informational);

				try
				{
					string text = await _speechManager.StopRecordingAndTranscribeAsync(_activeSpeechService, string.Empty, CancellationToken.None);
					string formatted = _transcriptFormatter.ApplyRules(text, false);

					TxtSpeechToText.Text = text;
					TxtFormatTranscript.Text = formatted;

					UpdateSpeechButtonVisuals("Start recording", RecordGlyph);
					BtnSpeechToText.IsEnabled = true;

					_clipboard.SetText(formatted);
					InsertIntoActiveApplication(formatted);

					BeepPlayer.Play(BeepType.Success);
					TxtSpeechToText.IsReadOnly = false;
					_suppressAutoActions = false;
					ShowStatus("Speech to Text", "Transcript ready and copied.", InfoBarSeverity.Success);
					HotkeyManager.SendHotkeyAfterDelay(_settings.SpeetchToTextSettings?.SendKotKeyAfterTranscriptionOperation, Constants.SendHotkeyDelay);
				}
				catch (OperationCanceledException)
				{
					UpdateSpeechButtonVisuals("Start recording", RecordGlyph);
					BtnSpeechToText.IsEnabled = true;
					TxtSpeechToText.IsReadOnly = false;
					_suppressAutoActions = false;
					ShowStatus("Speech to Text", "Transcription cancelled.", InfoBarSeverity.Warning);
					return;
				}
			}
		}
		catch (Exception ex)
		{
			ShowStatus("Speech to Text", ex.Message, InfoBarSeverity.Error);
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
		AutomationProperties.SetName(dialog, title);
		AutomationProperties.SetHelpText(dialog, message);

		await dialog.ShowAsync();
	}

	public void BtnTextToSpeech_Click(object? sender, RoutedEventArgs? e)
	{
		string text = _clipboard.GetText();
		_textToSpeech.SpeakText(text);
		ShowStatus("Text to Speech", "Speaking clipboard text.", InfoBarSeverity.Informational);
	}

	public void BtnFormatTranscript_Click(object? sender, RoutedEventArgs? e)
	{
		string raw = TxtSpeechToText.Text;
		string formatted = _transcriptFormatter.ApplyRules(raw, false);
		TxtFormatTranscript.Text = formatted;
		_clipboard.SetText(formatted);
		InsertIntoActiveApplication(formatted);
		BeepPlayer.Play(BeepType.Success);
		ShowStatus("Formatting", "Transcript formatted and copied.", InfoBarSeverity.Success);
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
			ShowStatus("Formatting", "Transcript refined with the language model.", InfoBarSeverity.Success);
		}
		catch (Exception ex)
		{
			ShowStatus("Formatting", ex.Message, InfoBarSeverity.Error);
			await ShowErrorDialog("Format with LLM Error", ex);
		}
	}

	private void UpdateMicrophoneToggleVisuals()
	{
		bool muted = _audioDeviceManager.IsMuted;
		BtnToggleMicLabel.Text = muted ? "Unmute microphone" : "Mute microphone";
		BtnToggleMicIcon.Glyph = muted ? MicOffGlyph : MicOnGlyph;
		AutomationProperties.SetName(BtnToggleMic, BtnToggleMicLabel.Text);
		AutomationProperties.SetHelpText(BtnToggleMic, BtnToggleMicLabel.Text);
		ToolTipService.SetToolTip(BtnToggleMic, BtnToggleMicLabel.Text);
	}

	private void UpdateSpeechButtonVisuals(string label, string glyph, bool isEnabled = true)
	{
		BtnSpeechToTextLabel.Text = label;
		BtnSpeechToTextIcon.Glyph = glyph;
		BtnSpeechToText.IsEnabled = isEnabled;
		AutomationProperties.SetName(BtnSpeechToText, label);
		AutomationProperties.SetHelpText(BtnSpeechToText, label);
		ToolTipService.SetToolTip(BtnSpeechToText, label);
	}

	private void ShowStatus(string title, string message, InfoBarSeverity severity)
	{
		void Update()
		{
			StatusInfoBar.Title = title;
			StatusInfoBar.Message = message;
			StatusInfoBar.Severity = severity;
			StatusInfoBar.Visibility = Visibility.Visible;
			StatusInfoBar.IsOpen = true;
			AutomationProperties.SetName(StatusInfoBar, $"{title} status");
			AutomationProperties.SetHelpText(StatusInfoBar, message);
			_statusDismissTimer.Stop();
			_statusDismissTimer.Start();
		}

		if (DispatcherQueue.HasThreadAccess)
			Update();
		else
			DispatcherQueue.TryEnqueue(Update);
	}

	private void StatusDismissTimer_Tick(object? sender, object e)
	{
		_statusDismissTimer.Stop();
		StatusInfoBar.IsOpen = false;
		StatusInfoBar.Visibility = Visibility.Collapsed;
	}

	private void StatusInfoBar_CloseButtonClick(InfoBar sender, object args)
	{
		_statusDismissTimer.Stop();
		StatusInfoBar.IsOpen = false;
		StatusInfoBar.Visibility = Visibility.Collapsed;
	}




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
		AutomationProperties.SetName(dialog, title);
		AutomationProperties.SetHelpText(dialog, message);
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

}
