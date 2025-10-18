﻿using CognitiveSupport;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Mutation.Ui.Services;
using Mutation.Ui.Views;
using NAudio.Wave;
using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;


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
	private HotkeyManager? _hotkeyManager;
        private readonly MediaPlayer _playbackPlayer;
        private bool _isPlayingRecording;
        private InMemoryRandomAccessStream? _playbackStream; // holds in-memory audio during playback to avoid locking the file
        private readonly List<SpeechSession> _sessionHistory = new();
        private string? _selectedSessionPath;
        private SpeechSession? _playingSession;

	private WaveInEvent? _waveformCapture;
	private DispatcherQueueTimer? _waveformTimer;
	// ScottPlot v5 renamed SignalPlot (v4) to Signal. Adjusting type accordingly.
	private Signal? _waveformSignal;
	private double[] _waveformBuffer = Array.Empty<double>();
	private double[] _waveformRenderBuffer = Array.Empty<double>();
	private int _waveformBufferIndex;
	private bool _waveformBufferFilled;
	private readonly object _waveformBufferLock = new();
	private double _waveformPeak;
	private double _waveformRms;
	private double _waveformPulse; // smoothed peak for visual pulse
	private bool VisualizationEnabled => _settings.AudioSettings?.EnableMicrophoneVisualization != false;

	// Suppress auto-format/clipboard/beep when we change text programmatically or during record/transcribe
	private bool _suppressAutoActions = false;

	private ISpeechToTextService? _activeSpeechService;
	private CancellationTokenSource _formatDebounceCts = new();
	private DictationInsertOption _insertOption = DictationInsertOption.Paste;
	private readonly DispatcherTimer _statusDismissTimer;
	private bool _hotkeyRouterInitialized;

	private static readonly IReadOnlyDictionary<string, string> AudioMimeTypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		[".aac"] = "audio/aac",
		[".flac"] = "audio/flac",
		[".m4a"] = "audio/mp4",
		[".mp3"] = "audio/mpeg",
		[".ogg"] = "audio/ogg",
		[".opus"] = "audio/opus",
		[".wav"] = "audio/wav",
		[".wma"] = "audio/x-ms-wma",
	};

	private const string MicOnGlyph = "\uE720";
	// '\uE7C8' is the Segoe MDL2 Assets glyph for a circular record icon, chosen for its clear visual representation.
	// Previously, '\uE768' was used, but '\uE7C8' better matches the standard record symbol.
	private const string RecordGlyph = "\uE7C8";
	private const string StopGlyph = "\uE71A";
	private const string ProcessingGlyph = "\uE8A0";
	private const string PlayGlyph = "\uE768";

	private const int WaveformSampleRate = 16_000;
	private const int WaveformWindowMilliseconds = 40;
	private const int WaveformWindowSampleCount = WaveformSampleRate * WaveformWindowMilliseconds / 1_000;
	private const double WaveformFrameIntervalMilliseconds = 1_000.0 / 30.0;
	private const int WaveformBufferMilliseconds = 15;

	private const string DoNotInsertExplanation = "Keep the transcript inside Mutation without sending it anywhere.";
	private const string SendKeysExplanation = "Types the transcript into the active app as if you entered it yourself.";
	private const string PasteExplanation = "Copies the transcript and pastes it into the active application.";
	private const double ApproximateLineHeightMultiplier = 1.35;
	private const double MinimumLineHeightInDips = 1.0;

	public ObservableCollection<HotkeyRouterEntry> HotkeyRouterEntries { get; } = new();
	private readonly List<(string From, string To)> _hotkeyRouterPersistedSnapshot = new();

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
		_playbackPlayer = new MediaPlayer { AutoPlay = false };
		_playbackPlayer.MediaEnded += PlaybackPlayer_MediaEnded;
                _playbackPlayer.MediaFailed += PlaybackPlayer_MediaFailed;

                InitializeComponent();
                InitializeMicrophoneVisualization();

                RefreshSessions();
                UpdatePlaybackButtonVisuals("Play selected session", PlayGlyph);
                AutomationProperties.SetHelpText(BtnRetrySpeechToText, "Transcribe the selected session again.");
                AutomationProperties.SetHelpText(BtnUploadSpeechAudio, "Upload an audio file for transcription.");
                AutomationProperties.SetHelpText(BtnSessionNewer, "Switch to a newer session.");
                AutomationProperties.SetHelpText(BtnSessionOlder, "Switch to an older session.");

		ApplyMultiLineTextBoxPreferences();

		_statusDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
		_statusDismissTimer.Tick += StatusDismissTimer_Tick;
		StatusInfoBar.CloseButtonClick += StatusInfoBar_CloseButtonClick;

		_audioDeviceManager.EnsureDefaultMicrophoneSelected();

		UpdateMicrophoneToggleVisuals();
		UpdateSpeechButtonVisuals("Start recording", RecordGlyph);
		var micList = _audioDeviceManager.CaptureDevices.ToList();
		CmbMicrophone.ItemsSource = micList;
		// DisplayMemberPath replaced by using custom CaptureDeviceComboItem if needed; keep for compatibility
		CmbMicrophone.DisplayMemberPath = nameof(CoreAudio.MMDevice.DeviceFriendlyName);

		RestorePersistedMicrophoneSelection(micList);
		StartMicrophoneVisualizationCapture();

		CmbSpeechService.ItemsSource = _speechServices;
		CmbSpeechService.DisplayMemberPath = nameof(ISpeechToTextService.ServiceName);

		RestorePersistedSpeechServiceSelection();
		UpdateRecordingActionAvailability();

		TxtFormatPrompt.Text = _settings.LlmSettings?.FormatTranscriptPrompt ?? string.Empty;

		var tooltipManager = new TooltipManager(_settings);
		tooltipManager.SetupTooltips(TxtSpeechToText, TxtFormatTranscript);

		CmbInsertOption.ItemsSource = Enum.GetValues(typeof(DictationInsertOption)).Cast<DictationInsertOption>().ToList();
		CmbInsertOption.SelectedItem = DictationInsertOption.Paste;
		UpdateThirdPartyExplanation(DictationInsertOption.Paste);

		// After initializing and restoring the active microphone, play a sound
		// representing the current state (mute/unmute) to reflect actual status.
		if (_audioDeviceManager.Microphone != null)
			BeepPlayer.Play(_audioDeviceManager.IsMuted ? BeepType.Mute : BeepType.Unmute);

		InitializeHotkeyVisuals();
		InitializeHotkeyRouter();

		this.Closed += MainWindow_Closed;
	}

	private void ApplyMultiLineTextBoxPreferences()
	{
		int configuredMaxLines = _settings.MainWindowUiSettings?.MaxTextBoxLineCount ?? 5;
		if (configuredMaxLines <= 0)
			configuredMaxLines = 5;

		foreach (var textBox in GetMultiLineTextBoxes())
		{
			if (textBox is null)
				continue;

			double lineHeight = Math.Max(textBox.FontSize * ApproximateLineHeightMultiplier, MinimumLineHeightInDips);
			double padding = textBox.Padding.Top + textBox.Padding.Bottom;
			double desiredMaxHeight = (lineHeight * configuredMaxLines) + padding;

			if (double.IsNaN(desiredMaxHeight) || double.IsInfinity(desiredMaxHeight) || desiredMaxHeight <= 0)
				continue;

			textBox.MaxHeight = desiredMaxHeight;

			if (textBox.MinHeight > desiredMaxHeight)
				textBox.MinHeight = lineHeight + padding;
		}
	}

	private IEnumerable<TextBox> GetMultiLineTextBoxes()
	{
		yield return TxtSpeechToText;
		yield return TxtFormatPrompt;
		yield return TxtFormatTranscript;
		yield return TxtOcr;
		yield return TxtClipboard;
	}

	public void AttachHotkeyManager(HotkeyManager hotkeyManager)
	{
		_hotkeyManager = hotkeyManager;
		RefreshHotkeyRouterRegistrations();
	}

	private void RestorePersistedSpeechServiceSelection()
	{
		string? savedServiceName = _settings.SpeechToTextSettings?.ActiveSpeechToTextService;
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

	private static string GetDeviceFriendlyName(CoreAudio.MMDevice device)
	{
#pragma warning disable CS0618
		var name = device.DeviceFriendlyName;
		if (string.IsNullOrWhiteSpace(name))
			name = device.FriendlyName;
#pragma warning restore CS0618
		return name ?? string.Empty;
	}

	private void RestorePersistedMicrophoneSelection(System.Collections.Generic.List<CoreAudio.MMDevice> micList)
	{
		string? savedMicFullName = _settings.AudioSettings?.ActiveCaptureDeviceFullName;
		if (!string.IsNullOrWhiteSpace(savedMicFullName))
		{
			var match = micList.FirstOrDefault(m => GetDeviceFriendlyName(m) == savedMicFullName);
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

	private void InitializeHotkeyVisuals()
	{
		ConfigureButtonHotkey(BtnToggleMic, BtnToggleMicHotkey, _settings.AudioSettings?.MicrophoneToggleMuteHotKey, BtnToggleMicLabel.Text);
		ConfigureButtonHotkey(BtnSpeechToText, BtnSpeechToTextHotkey, _settings.SpeechToTextSettings?.SpeechToTextHotKey, BtnSpeechToTextLabel.Text);
		ConfigureButtonHotkey(BtnScreenshot, BtnScreenshotHotkey, _settings.AzureComputerVisionSettings?.ScreenshotHotKey, "Copy a screenshot directly to the clipboard");
		ConfigureButtonHotkey(BtnOcrClipboard, BtnOcrClipboardHotkey, _settings.AzureComputerVisionSettings?.OcrHotKey, "Run OCR on an image stored in the clipboard");
		ConfigureButtonHotkey(BtnOcrClipboardLrtb, BtnOcrClipboardLrtbHotkey, _settings.AzureComputerVisionSettings?.OcrLeftToRightTopToBottomHotKey, "Run OCR on an image stored in the clipboard using left-to-right reading order");
		ConfigureButtonHotkey(BtnScreenshotOcr, BtnScreenshotOcrHotkey, _settings.AzureComputerVisionSettings?.ScreenshotOcrHotKey, "Capture a screenshot and extract text automatically");
		ConfigureButtonHotkey(BtnScreenshotOcrLrtb, BtnScreenshotOcrLrtbHotkey, _settings.AzureComputerVisionSettings?.ScreenshotLeftToRightTopToBottomOcrHotKey, "Capture a screenshot and extract text using left-to-right reading order");
		ConfigureButtonHotkey(BtnTextToSpeech, BtnTextToSpeechHotkey, _settings.TextToSpeechSettings?.TextToSpeechHotKey, "Play the clipboard text using text-to-speech");
	}

	private void InitializeHotkeyRouter()
	{
		_hotkeyRouterInitialized = false;

		_settings.HotKeyRouterSettings ??= new HotKeyRouterSettings();

		foreach (var entry in HotkeyRouterEntries)
			DetachHotkeyRouterEntry(entry);
		HotkeyRouterEntries.Clear();
		foreach (var map in _settings.HotKeyRouterSettings.Mappings)
		{
			var entry = new HotkeyRouterEntry(map);
			AttachHotkeyRouterEntry(entry);
			HotkeyRouterEntries.Add(entry);
		}

		var initialPairs = HotkeyRouterEntries
				  .Where(e => e.IsValid && e.NormalizedFromHotkey is not null && e.NormalizedToHotkey is not null)
				  .Select(e => (From: e.NormalizedFromHotkey!, To: e.NormalizedToHotkey!))
				  .ToList();
		UpdateHotkeyRouterSnapshot(initialPairs);

		RecalculateHotkeyRouterDuplicates();
		// Defer registration & persistence until HotkeyManager is attached to avoid
		// any chance of wiping persisted mappings during initial construction.
		_hotkeyRouterInitialized = true;
	}

	private void InitializeMicrophoneVisualization()
	{
		if (MicWaveformPlot is null)
			return;

		_waveformBuffer = new double[WaveformWindowSampleCount];
		_waveformRenderBuffer = new double[WaveformWindowSampleCount];
		_waveformBufferIndex = 0;
		_waveformBufferFilled = false;

		var plot = MicWaveformPlot.Plot;
		plot.Clear();
		_waveformSignal = plot.Add.Signal(_waveformRenderBuffer);
		plot.Axes.SetLimitsX(0, Math.Max(1, WaveformWindowSampleCount - 1));
		plot.Axes.SetLimitsY(-1, 1);
		plot.HideGrid();
		MicWaveformPlot.Refresh();
		if (_settings.AudioSettings != null)
		{
			if (!VisualizationEnabled)
			{
				MicWaveformPlot.Visibility = Visibility.Collapsed;
				if (MicWaveformOffLabel != null) MicWaveformOffLabel.Visibility = Visibility.Visible;
			}
		}

		_waveformTimer = DispatcherQueue.CreateTimer();
		_waveformTimer.Interval = TimeSpan.FromMilliseconds(WaveformFrameIntervalMilliseconds);
		_waveformTimer.Tick += WaveformTimer_Tick;
		_waveformTimer.Start();
	}

	private void WaveformTimer_Tick(DispatcherQueueTimer sender, object args)
	{
		if (!VisualizationEnabled)
		{
			if (MicWaveformPlot.Visibility != Visibility.Collapsed)
				MicWaveformPlot.Visibility = Visibility.Collapsed;
			if (MicWaveformOffLabel != null)
				MicWaveformOffLabel.Visibility = Visibility.Visible;
			if (RmsLevelBar != null)
				RmsLevelBar.Height = 0;
			return;
		}

		if (MicWaveformPlot.Visibility != Visibility.Visible)
			MicWaveformPlot.Visibility = Visibility.Visible;
		if (MicWaveformOffLabel != null && MicWaveformOffLabel.Visibility == Visibility.Visible)
			MicWaveformOffLabel.Visibility = Visibility.Collapsed;

		int validSamples = PopulateWaveformRenderBuffer();

                double peak = 0;
                double sumSquares = 0;
                if (validSamples > 0)
                {
                        int samplesToProcess = Math.Min(validSamples, _waveformRenderBuffer.Length);
                        int startIndex = _waveformRenderBuffer.Length - samplesToProcess;
                        for (int i = startIndex; i < _waveformRenderBuffer.Length; i++)
                        {
                                double value = _waveformRenderBuffer[i];
                                double abs = Math.Abs(value);
                                if (abs > peak)
                                        peak = abs;
                                sumSquares += value * value;
                        }
                        _waveformRms = Math.Sqrt(sumSquares / Math.Max(1, samplesToProcess));
                }
		else
		{
			_waveformRms = 0;
		}

		_waveformPeak = peak;

		if (_waveformSignal != null)
			MicWaveformPlot.Refresh();

		UpdateMicLevelMeter(peak, _waveformRms);

		_waveformPulse = Math.Max(_waveformPulse * 0.85, Math.Min(1.0, peak));
		if (MicPulseOverlay != null)
			MicPulseOverlay.Opacity = _waveformPulse * 0.35;
	}

	private int PopulateWaveformRenderBuffer()
	{
		if (_waveformRenderBuffer.Length == 0 || _waveformBuffer.Length == 0)
		{
			return 0;
		}

		lock (_waveformBufferLock)
		{
			if (!_waveformBufferFilled && _waveformBufferIndex == 0)
			{
				Array.Clear(_waveformRenderBuffer, 0, _waveformRenderBuffer.Length);
				return 0;
			}

                        if (_waveformBufferFilled)
                        {
                                int bufferLen = _waveformRenderBuffer.Length;
                                int index = _waveformBufferIndex;
                                if (index > bufferLen)
                                        index = bufferLen;
                                int tailLength = bufferLen - index;
                                if (tailLength > 0)
                                        Array.Copy(_waveformBuffer, index, _waveformRenderBuffer, 0, tailLength);
                                if (index > 0)
                                        Array.Copy(_waveformBuffer, 0, _waveformRenderBuffer, tailLength, index);
                                return bufferLen;
                        }

			int validCount = _waveformBufferIndex;
			int leadingZeros = _waveformRenderBuffer.Length - validCount;
			if (leadingZeros > 0)
				Array.Clear(_waveformRenderBuffer, 0, leadingZeros);
			Array.Copy(_waveformBuffer, 0, _waveformRenderBuffer, Math.Max(0, leadingZeros), validCount);
			return validCount;
		}
	}

	private void UpdateMicLevelMeter(double peak, double rms)
	{
		if (RmsLevelBar is null || MicWaveformPlot is null)
			return;

		double waveformHeight = MicWaveformPlot.ActualHeight;
		if (double.IsNaN(waveformHeight) || waveformHeight <= 0)
			waveformHeight = MicWaveformPlot.Height;

		if (MicLevelMeter is not null && waveformHeight > 0)
			MicLevelMeter.Height = waveformHeight;

                double levelValue = rms;
		levelValue = Math.Min(1.0, Math.Max(0, levelValue));

		RmsLevelBar.Height = waveformHeight * levelValue;
	}

	private void StartMicrophoneVisualizationCapture()
	{
		if (_waveformRenderBuffer.Length == 0)
			return;

		StopMicrophoneVisualizationCapture();

		lock (_waveformBufferLock)
		{
			if (_waveformBuffer.Length > 0)
				Array.Clear(_waveformBuffer, 0, _waveformBuffer.Length);
			if (_waveformRenderBuffer.Length > 0)
				Array.Clear(_waveformRenderBuffer, 0, _waveformRenderBuffer.Length);
			_waveformBufferIndex = 0;
			_waveformBufferFilled = false;
		}

		int deviceIndex = _audioDeviceManager.MicrophoneDeviceIndex;
		if (deviceIndex < 0)
		{
			// Provide a visible hint if selection failed to map to an NAudio device index.
			DispatcherQueue.TryEnqueue(() =>
					  ShowStatus("Microphone", "Unable to start waveform monitor (device not resolved)", InfoBarSeverity.Warning));
			return;
		}

		try
		{
			_waveformCapture = new WaveInEvent
			{
				DeviceNumber = deviceIndex,
				WaveFormat = new WaveFormat(WaveformSampleRate, 16, 1),
				BufferMilliseconds = WaveformBufferMilliseconds
			};
			_waveformCapture.DataAvailable += OnWaveformDataAvailable;
			_waveformCapture.StartRecording();
		}
		catch (Exception ex)
		{
			_waveformCapture?.Dispose();
			_waveformCapture = null;
			DispatcherQueue.TryEnqueue(() =>
					  ShowStatus("Microphone", $"Unable to monitor audio: {ex.Message}", InfoBarSeverity.Error));
		}
	}

	private void RestartMicrophoneVisualizationCapture()
	{
		StopMicrophoneVisualizationCapture();
		StartMicrophoneVisualizationCapture();
	}

	private void StopMicrophoneVisualizationCapture()
	{
		if (_waveformCapture is null)
			return;

		try
		{
			_waveformCapture.DataAvailable -= OnWaveformDataAvailable;
			_waveformCapture.StopRecording();
		}
		catch
		{
			// Ignore failures that occur while shutting down capture.
		}

		_waveformCapture.Dispose();
		_waveformCapture = null;
	}

	private void DisposeMicrophoneVisualization()
	{
		StopMicrophoneVisualizationCapture();

		if (_waveformTimer is not null)
		{
			_waveformTimer.Tick -= WaveformTimer_Tick;
			_waveformTimer.Stop();
			_waveformTimer = null;
		}

		_waveformSignal = null;
		_waveformBuffer = Array.Empty<double>();
		_waveformRenderBuffer = Array.Empty<double>();
		_waveformBufferIndex = 0;
		_waveformBufferFilled = false;
	}

	private void OnWaveformDataAvailable(object? sender, WaveInEventArgs e)
	{
		if (_waveformBuffer.Length == 0 || e.BytesRecorded <= 0)
			return;

		int sampleCount = e.BytesRecorded / 2;
		if (sampleCount <= 0)
			return;

		lock (_waveformBufferLock)
		{
			for (int i = 0; i < sampleCount; i++)
			{
				short sample = BitConverter.ToInt16(e.Buffer, i * 2);
				double value = sample / 32768d;
				_waveformBuffer[_waveformBufferIndex++] = value;
				if (_waveformBufferIndex >= _waveformBuffer.Length)
				{
					_waveformBufferIndex = 0;
					_waveformBufferFilled = true;
				}
			}
		}
	}

	private void RefreshHotkeyRouterRegistrations()
	{
		_settings.HotKeyRouterSettings ??= new HotKeyRouterSettings();

		RecalculateHotkeyRouterDuplicates();
		var normalizedPairs = SyncHotkeyRouterSettings();

		if (_hotkeyManager is null)
		{
			foreach (var entry in HotkeyRouterEntries)
				entry.SetBindingResult(HotkeyBindingState.Inactive, null);

			if (ShouldPersistHotkeyRouterMappings(normalizedPairs))
			{
				_settingsManager.SaveSettingsToFile(_settings);
				UpdateHotkeyRouterSnapshot(normalizedPairs);
			}
			return;
		}

		var mappings = _settings.HotKeyRouterSettings.Mappings;
		var results = _hotkeyManager.RefreshRouterHotkeys(mappings);
		var resultLookup = results.ToDictionary(r => r.Map);

		foreach (var entry in HotkeyRouterEntries)
		{
			if (resultLookup.TryGetValue(entry.Map, out var result))
			{
				entry.SetBindingResult(result.Success ? HotkeyBindingState.Bound : HotkeyBindingState.Failed, result.ErrorMessage);
			}
			else
			{
				entry.SetBindingResult(HotkeyBindingState.Inactive, null);
			}
		}

		if (ShouldPersistHotkeyRouterMappings(normalizedPairs))
		{
			_settingsManager.SaveSettingsToFile(_settings);
			UpdateHotkeyRouterSnapshot(normalizedPairs);
		}
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
			_settings.SpeechToTextSettings!.ActiveSpeechToTextService = _activeSpeechService.ServiceName;
		_settings.LlmSettings!.FormatTranscriptPrompt = TxtFormatPrompt.Text;

		var normalizedPairs = SyncHotkeyRouterSettings();
		_settingsManager.SaveSettingsToFile(_settings);
		UpdateHotkeyRouterSnapshot(normalizedPairs);
		StopPlayback();
		_playbackPlayer.MediaEnded -= PlaybackPlayer_MediaEnded;
		_playbackPlayer.MediaFailed -= PlaybackPlayer_MediaFailed;
		_playbackPlayer.Dispose();
		BeepPlayer.DisposePlayers();
		DisposeMicrophoneVisualization();
	}

	private void CopyText_Click(object sender, RoutedEventArgs e)
	{
		_clipboard.SetText(TxtClipboard.Text);
		ShowStatus("Clipboard", "Text copied to the clipboard.", InfoBarSeverity.Success);
	}

	private void BtnAddHotkeyRoute_Click(object sender, RoutedEventArgs e)
	{
		_settings.HotKeyRouterSettings ??= new HotKeyRouterSettings();

		var map = new HotKeyRouterSettings.HotKeyRouterMap(string.Empty, string.Empty);

		var entry = new HotkeyRouterEntry(map);
		AttachHotkeyRouterEntry(entry);
		HotkeyRouterEntries.Add(entry);

		RefreshHotkeyRouterRegistrations();

		// Defer focusing until the ListView generates the container
		TryFocusHotkeyRouterFromTextBox(entry);
	}

	private void HotkeyRouterDelete_Click(object sender, RoutedEventArgs e)
	{
		if (((FrameworkElement)sender).Tag is not HotkeyRouterEntry entry)
			return;

		if (_settings.HotKeyRouterSettings is not null)
			_settings.HotKeyRouterSettings.Mappings.Remove(entry.Map);

		DetachHotkeyRouterEntry(entry);
		HotkeyRouterEntries.Remove(entry);
		RefreshHotkeyRouterRegistrations();
	}

	private void HotkeyRouterFrom_LostFocus(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: HotkeyRouterEntry entry })
		{
			entry.CommitFromHotkey();
			RefreshHotkeyRouterRegistrations();
		}
	}

	private void HotkeyRouterTo_LostFocus(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: HotkeyRouterEntry entry })
		{
			entry.CommitToHotkey();
			RefreshHotkeyRouterRegistrations();
		}
	}

	private void HotkeyRouterEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not HotkeyRouterEntry)
			return;

		if (e.PropertyName == nameof(HotkeyRouterEntry.FromHotkey) || e.PropertyName == nameof(HotkeyRouterEntry.IsFromValid))
			RecalculateHotkeyRouterDuplicates();
	}

	private void AttachHotkeyRouterEntry(HotkeyRouterEntry entry)
	{
		entry.PropertyChanged += HotkeyRouterEntry_PropertyChanged;
	}

	private void TryFocusHotkeyRouterFromTextBox(HotkeyRouterEntry entry)
	{
		// Run async attempts on dispatcher without blocking UI thread
		DispatcherQueue.TryEnqueue(async () =>
		{
			for (int i = 0; i < 8; i++)
			{
				var container = HotkeyRouterList.ContainerFromItem(entry) as ListViewItem;
				if (container?.ContentTemplateRoot is FrameworkElement root)
				{
					// First TextBox inside the template corresponds to the 'From' hotkey
					var fromTextBox = FindDescendant<TextBox>(root);
					if (fromTextBox != null)
					{
						fromTextBox.Focus(FocusState.Programmatic);
						// Select existing text (if any) to allow immediate typing
						fromTextBox.SelectAll();
						return;
					}
				}
				await Task.Delay(40);
			}
		});
	}

	private static T? FindDescendant<T>(DependencyObject root) where T : class
	{
		int count = VisualTreeHelper.GetChildrenCount(root);
		for (int i = 0; i < count; i++)
		{
			var child = VisualTreeHelper.GetChild(root, i);
			if (child is T typed)
				return typed;
			var result = FindDescendant<T>(child);
			if (result != null)
				return result;
		}
		return null;
	}

	private void DetachHotkeyRouterEntry(HotkeyRouterEntry entry)
	{
		entry.PropertyChanged -= HotkeyRouterEntry_PropertyChanged;
	}

	private void RecalculateHotkeyRouterDuplicates()
	{
		var duplicates = HotkeyRouterEntries
				  .Where(e => e.IsFromValid && e.NormalizedFromHotkey is not null)
				  .GroupBy(e => e.NormalizedFromHotkey!, StringComparer.OrdinalIgnoreCase)
				  .Where(g => g.Count() > 1)
				  .SelectMany(g => g);

		var duplicateSet = new HashSet<HotkeyRouterEntry>(duplicates);

		foreach (var entry in HotkeyRouterEntries)
			entry.SetDuplicate(duplicateSet.Contains(entry));
	}

	private List<(string From, string To)> SyncHotkeyRouterSettings()
	{
		_settings.HotKeyRouterSettings ??= new HotKeyRouterSettings();

		foreach (var entry in HotkeyRouterEntries)
		{
			entry.CommitFromHotkey();
			entry.CommitToHotkey();
		}

		var validEntries = HotkeyRouterEntries
				  .Where(e => e.IsValid && e.NormalizedFromHotkey is not null && e.NormalizedToHotkey is not null)
				  .ToList();

		// If no entries are currently valid but existing settings contain mappings, preserve them.
		// This avoids wiping user settings due to a transient validation state during startup.
		if (validEntries.Count == 0 && _settings.HotKeyRouterSettings.Mappings.Count > 0)
		{
			return _settings.HotKeyRouterSettings.Mappings
					  .Where(m => !string.IsNullOrWhiteSpace(m.FromHotKey) && !string.IsNullOrWhiteSpace(m.ToHotKey))
					  .Select(m => (From: m.FromHotKey!, To: m.ToHotKey!))
					  .ToList();
		}

		var normalizedPairs = validEntries
				  .Select(e => (From: e.NormalizedFromHotkey!, To: e.NormalizedToHotkey!))
				  .ToList();

		var existing = _settings.HotKeyRouterSettings.Mappings;

		bool changed = existing.Count != normalizedPairs.Count;
		if (!changed)
		{
			for (int i = 0; i < existing.Count; i++)
			{
				var existingFrom = existing[i].FromHotKey ?? string.Empty;
				var existingTo = existing[i].ToHotKey ?? string.Empty;

				if (!string.Equals(existingFrom, normalizedPairs[i].From, StringComparison.Ordinal) ||
					 !string.Equals(existingTo, normalizedPairs[i].To, StringComparison.Ordinal))
				{
					changed = true;
					break;
				}
			}
		}

		if (changed)
		{
			var updatedMaps = normalizedPairs
					  .Select(pair => new HotKeyRouterSettings.HotKeyRouterMap(pair.From, pair.To))
					  .ToList();

			_settings.HotKeyRouterSettings.Mappings = updatedMaps;

			for (int i = 0; i < validEntries.Count; i++)
				validEntries[i].ReplaceBackingMap(updatedMaps[i]);
		}

		return normalizedPairs;
	}

	private bool ShouldPersistHotkeyRouterMappings(List<(string From, string To)> normalizedPairs)
	{
		if (!_hotkeyRouterInitialized)
			return false;

		if (_hotkeyRouterPersistedSnapshot.Count != normalizedPairs.Count)
			return true;

		for (int i = 0; i < normalizedPairs.Count; i++)
		{
			var previous = _hotkeyRouterPersistedSnapshot[i];
			var current = normalizedPairs[i];

			if (!string.Equals(previous.From, current.From, StringComparison.Ordinal) ||
				 !string.Equals(previous.To, current.To, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private void UpdateHotkeyRouterSnapshot(IEnumerable<(string From, string To)> normalizedPairs)
	{
		_hotkeyRouterPersistedSnapshot.Clear();
		_hotkeyRouterPersistedSnapshot.AddRange(normalizedPairs);
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
			HotkeyManager.SendHotkeyAfterDelay(_settings.AzureComputerVisionSettings?.SendHotkeyAfterOcrOperation, result.Success ? Constants.SendHotkeyDelay : Constants.FailureSendHotkeyDelay);
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

	private async void BtnScreenshotOcrLrtb_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			var result = await _ocrManager.TakeScreenshotAndExtractTextAsync(OcrReadingOrder.LeftToRightTopToBottom);
			SetOcrText(result.Message);
			HotkeyManager.SendHotkeyAfterDelay(_settings.AzureComputerVisionSettings?.SendHotkeyAfterOcrOperation, result.Success ? Constants.SendHotkeyDelay : Constants.FailureSendHotkeyDelay);
			if (result.Success)
				ShowStatus("Screenshot & OCR (left-to-right)", "Text captured from screenshot using left-to-right reading order.", InfoBarSeverity.Success);
			else
				ShowStatus("Screenshot & OCR (left-to-right)", result.Message, InfoBarSeverity.Error);
		}
		catch (Exception ex)
		{
			ShowStatus("Screenshot & OCR (left-to-right)", ex.Message, InfoBarSeverity.Error);
			await ShowErrorDialog("Screenshot + OCR (LRTB) Error", ex);
		}
	}

	private async void BtnOcrClipboard_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			var result = await _ocrManager.ExtractTextFromClipboardImageAsync(OcrReadingOrder.TopToBottomColumnAware);
			SetOcrText(result.Message);
			HotkeyManager.SendHotkeyAfterDelay(_settings.AzureComputerVisionSettings?.SendHotkeyAfterOcrOperation ?? string.Empty, result.Success ? Constants.SendHotkeyDelay : Constants.FailureSendHotkeyDelay);
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
		try
		{
			await StartStopSpeechToTextAsync();
		}
		catch (Exception ex)
		{
			ShowStatus("Speech to Text", ex.Message, InfoBarSeverity.Error);
			await ShowErrorDialog("Speech to Text Error", ex);
		}
	}

        private async void BtnPlayLatestRecording_Click(object? sender, RoutedEventArgs? e)
        {
                try
                {
                        var session = GetSelectedSession();
                        if (session is null)
                        {
                                ShowStatus("Speech to Text", "No session is available for playback.", InfoBarSeverity.Warning);
                                UpdateRecordingActionAvailability();
                                return;
                        }

                        if (_isPlayingRecording && _playingSession != null && PathsEqual(_playingSession.FilePath, session.FilePath))
                        {
                                StopPlayback();
                        }
                        else
                        {
                                await StartPlaybackAsync(session);
                        }
                }
                catch (Exception ex)
                {
                        StopPlayback();
                        ShowStatus("Speech to Text", ex.Message, InfoBarSeverity.Error);
			await ShowErrorDialog("Playback Error", ex);
                }
        }

        private async void BtnSessionNewer_Click(object? sender, RoutedEventArgs? e)
        {
                try
                {
                        await NavigateSessionsAsync(-1);
                }
                catch (Exception ex)
                {
                        StopPlayback();
                        ShowStatus("Speech to Text", ex.Message, InfoBarSeverity.Error);
                        await ShowErrorDialog("Playback Error", ex);
                }
        }

        private async void BtnSessionOlder_Click(object? sender, RoutedEventArgs? e)
        {
                try
                {
                        await NavigateSessionsAsync(1);
                }
                catch (Exception ex)
                {
                        StopPlayback();
                        ShowStatus("Speech to Text", ex.Message, InfoBarSeverity.Error);
                        await ShowErrorDialog("Playback Error", ex);
                }
        }

        private async Task NavigateSessionsAsync(int direction)
        {
                if (_speechManager.Recording || _speechManager.Transcribing)
                        return;

                RefreshSessions(preferredPath: _selectedSessionPath);

                if (_sessionHistory.Count == 0)
                        return;

                int currentIndex = GetSelectedSessionIndex();
                if (currentIndex < 0)
                        currentIndex = 0;

                int targetIndex = direction < 0 ? currentIndex - 1 : currentIndex + 1;
                if (targetIndex < 0 || targetIndex >= _sessionHistory.Count)
                        return;

                var targetSession = _sessionHistory[targetIndex];

                StopPlayback();
                _selectedSessionPath = targetSession.FilePath;
                UpdateSessionNavigationAvailability();
                UpdateRecordingActionAvailability();

                await StartPlaybackAsync(targetSession);
        }

        private async void BtnRetrySpeechToText_Click(object? sender, RoutedEventArgs? e)
        {
                if (_speechManager.Recording || _speechManager.Transcribing)
                {
                        ShowStatus("Speech to Text", "Finish the current operation before retrying.", InfoBarSeverity.Warning);
			UpdateRecordingActionAvailability();
			return;
		}

		if (_activeSpeechService == null)
		{
			ShowStatus("Speech to Text", "Select a speech-to-text service to retry.", InfoBarSeverity.Warning);
			UpdateRecordingActionAvailability();
			return;
		}

                var sessionToRetry = GetSelectedSession();
                if (sessionToRetry is null)
                {
                        ShowStatus("Speech to Text", "No session available to retry.", InfoBarSeverity.Warning);
                        UpdateRecordingActionAvailability();
                        return;
                }

                try
                {
                        StopPlayback();

                        _suppressAutoActions = true;
			TxtSpeechToText.IsReadOnly = true;
			TxtSpeechToText.Text = "Transcribing...";
			UpdateSpeechButtonVisuals("Transcribing...", ProcessingGlyph, false);
			UpdateRecordingActionAvailability();
			ShowStatus("Speech to Text", "Transcribing your recording...", InfoBarSeverity.Informational);

                        string text = await _speechManager.TranscribeExistingRecordingAsync(_activeSpeechService!, sessionToRetry, string.Empty, CancellationToken.None);

                        UpdateSpeechButtonVisuals("Start recording", RecordGlyph);
                        FinalizeTranscript(text, "Transcript refreshed from the selected session.");
                }
                catch (OperationCanceledException)
                {
                        UpdateSpeechButtonVisuals("Start recording", RecordGlyph);
                        TxtSpeechToText.IsReadOnly = false;
			_suppressAutoActions = false;
			ShowStatus("Speech to Text", "Transcription cancelled.", InfoBarSeverity.Warning);
			UpdateRecordingActionAvailability();
		}
		catch (Exception ex)
		{
			UpdateSpeechButtonVisuals("Start recording", RecordGlyph);
			TxtSpeechToText.IsReadOnly = false;
			_suppressAutoActions = false;
			UpdateRecordingActionAvailability();
			ShowStatus("Speech to Text", ex.Message, InfoBarSeverity.Error);
			await ShowErrorDialog("Speech to Text Error", ex);
		}
	}

	private async void BtnUploadSpeechAudio_Click(object? sender, RoutedEventArgs? e)
	{
		if (_speechManager.Recording || _speechManager.Transcribing)
		{
			ShowStatus("Speech to Text", "Finish the current operation before uploading.", InfoBarSeverity.Warning);
			UpdateRecordingActionAvailability();
			return;
		}

		if (_activeSpeechService == null)
		{
			ShowStatus("Speech to Text", "Select a speech-to-text service to transcribe audio.", InfoBarSeverity.Warning);
			UpdateRecordingActionAvailability();
			return;
		}

		var picker = new FileOpenPicker
		{
			SuggestedStartLocation = PickerLocationId.MusicLibrary,
			ViewMode = PickerViewMode.List
		};
		picker.FileTypeFilter.Add(".mp3");
		picker.FileTypeFilter.Add(".wav");
		picker.FileTypeFilter.Add(".m4a");
		picker.FileTypeFilter.Add(".aac");
		picker.FileTypeFilter.Add(".flac");
		picker.FileTypeFilter.Add(".ogg");
		picker.FileTypeFilter.Add(".opus");
		picker.FileTypeFilter.Add(".wma");
		picker.FileTypeFilter.Add(".webm");

		InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
		StorageFile? file = await picker.PickSingleFileAsync();
		if (file is null)
			return;

		try
		{
			StopPlayback();

			_suppressAutoActions = true;
			TxtSpeechToText.IsReadOnly = true;
			TxtSpeechToText.Text = "Transcribing...";
			UpdateSpeechButtonVisuals("Transcribing...", ProcessingGlyph, false);
			UpdateRecordingActionAvailability();
			ShowStatus("Speech to Text", $"Transcribing {file.Name}...", InfoBarSeverity.Informational);

                        var session = await _speechManager.ImportUploadedAudioAsync(file.Path, CancellationToken.None);
                        RefreshSessions(session);
                        UpdateRecordingActionAvailability();

                        string text = await _speechManager.TranscribeExistingRecordingAsync(_activeSpeechService, session, string.Empty, CancellationToken.None);

                        UpdateSpeechButtonVisuals("Start recording", RecordGlyph);
                        FinalizeTranscript(text, $"Transcript generated from {session.FileName}.");
                }
                catch (OperationCanceledException)
                {
                        UpdateSpeechButtonVisuals("Start recording", RecordGlyph);
                        TxtSpeechToText.IsReadOnly = false;
			_suppressAutoActions = false;
			UpdateRecordingActionAvailability();
			ShowStatus("Speech to Text", "Transcription cancelled.", InfoBarSeverity.Warning);
		}
		catch (Exception ex)
		{
			UpdateSpeechButtonVisuals("Start recording", RecordGlyph);
			TxtSpeechToText.IsReadOnly = false;
			_suppressAutoActions = false;
			UpdateRecordingActionAvailability();
			ShowStatus("Speech to Text", ex.Message, InfoBarSeverity.Error);
			await ShowErrorDialog("Speech to Text Error", ex);
		}
	}

	private async void BtnOcrClipboardLrtb_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			var result = await _ocrManager.ExtractTextFromClipboardImageAsync(OcrReadingOrder.LeftToRightTopToBottom);
			SetOcrText(result.Message);
			HotkeyManager.SendHotkeyAfterDelay(_settings.AzureComputerVisionSettings?.SendHotkeyAfterOcrOperation ?? string.Empty, result.Success ? Constants.SendHotkeyDelay : Constants.FailureSendHotkeyDelay);
			if (result.Success)
				ShowStatus("OCR (left-to-right)", "Clipboard image converted using left-to-right reading order.", InfoBarSeverity.Success);
			else
				ShowStatus("OCR (left-to-right)", result.Message, InfoBarSeverity.Warning);
		}
		catch (Exception ex)
		{
			ShowStatus("OCR (left-to-right)", ex.Message, InfoBarSeverity.Error);
			await ShowErrorDialog("OCR Clipboard (LRTB) Error", ex);
		}
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
				UpdateRecordingActionAvailability();
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
                        StopPlayback();
                        var session = await _speechManager.StartRecordingAsync(_audioDeviceManager.MicrophoneDeviceIndex);
                        RefreshSessions(session);
                        _suppressAutoActions = false;
                        UpdateRecordingActionAvailability();
                }
                else
                {
				BtnSpeechToText.IsEnabled = false;
				_suppressAutoActions = true;
				TxtSpeechToText.Text = "Transcribing...";
				UpdateSpeechButtonVisuals("Transcribing...", ProcessingGlyph, false);
				ShowStatus("Speech to Text", "Transcribing your recording...", InfoBarSeverity.Informational);
				StopPlayback();
				UpdateRecordingActionAvailability();

                                try
                                {
                                        string text = await _speechManager.StopRecordingAndTranscribeAsync(_activeSpeechService, string.Empty, CancellationToken.None);

                                        UpdateSpeechButtonVisuals("Start recording", RecordGlyph);
                                        BtnSpeechToText.IsEnabled = true;
                                        FinalizeTranscript(text, "Transcript ready and copied.");
                                }
				catch (OperationCanceledException)
				{
					UpdateSpeechButtonVisuals("Start recording", RecordGlyph);
					BtnSpeechToText.IsEnabled = true;
					TxtSpeechToText.IsReadOnly = false;
					_suppressAutoActions = false;
					ShowStatus("Speech to Text", "Transcription cancelled.", InfoBarSeverity.Warning);
					UpdateRecordingActionAvailability();
					return;
				}
				UpdateRecordingActionAvailability();
			}
		}
		catch (Exception ex)
		{
			ShowStatus("Speech to Text", ex.Message, InfoBarSeverity.Error);
			await ShowErrorDialog("Speech to Text Error", ex);
			UpdateRecordingActionAvailability();
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
		BtnToggleMicIcon.Glyph = MicOnGlyph;
		BtnToggleMicSlash.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
		AutomationProperties.SetName(BtnToggleMic, BtnToggleMicLabel.Text);
		ConfigureButtonHotkey(BtnToggleMic, BtnToggleMicHotkey, _settings.AudioSettings?.MicrophoneToggleMuteHotKey, BtnToggleMicLabel.Text);
		MicStatusIcon.Glyph = MicOnGlyph;
		MicStatusIconSlash.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
		MicStatusIcon.Foreground = ResolveBrush(muted ? "TextFillColorSecondaryBrush" : "TextFillColorPrimaryBrush");
		ToolTipService.SetToolTip(MicStatusIcon, muted ? "Microphone muted" : "Microphone live");
		AutomationProperties.SetName(MicStatusIcon, muted ? "Microphone muted" : "Microphone live");
	}

	private static Brush ResolveBrush(string resourceKey)
	{
		if (Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is Brush brush)
			return brush;

		// Fallback to a neutral gray if the requested resource isn't found. In WinUI 3 the Colors struct lives under Microsoft.UI.
		return Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush
			?? new SolidColorBrush(Microsoft.UI.Colors.Gray);
	}

	private void UpdateSpeechButtonVisuals(string label, string glyph, bool isEnabled = true)
	{
		BtnSpeechToTextLabel.Text = label;
		BtnSpeechToTextIcon.Glyph = glyph;
		BtnSpeechToText.IsEnabled = isEnabled;
		AutomationProperties.SetName(BtnSpeechToText, label);
		ConfigureButtonHotkey(BtnSpeechToText, BtnSpeechToTextHotkey, _settings.SpeechToTextSettings?.SpeechToTextHotKey, label);
	}

        private void UpdatePlaybackButtonVisuals(string automationName, string glyph)
        {
                BtnPlayLatestRecordingIcon.Glyph = glyph;
                string tooltip = automationName == "Play selected session"
                                  ? "Play the selected session"
                                  : "Stop playing the selected session";
                ToolTipService.SetToolTip(BtnPlayLatestRecording, tooltip);
                AutomationProperties.SetName(BtnPlayLatestRecording, automationName);
                AutomationProperties.SetHelpText(BtnPlayLatestRecording, tooltip);
        }

        private void RefreshSessions(SpeechSession? preferredSelection = null, string? preferredPath = null)
        {
                var snapshot = _speechManager.GetSessions();
                _sessionHistory.Clear();
                _sessionHistory.AddRange(snapshot);

                if (preferredSelection != null)
                {
                        _selectedSessionPath = preferredSelection.FilePath;
                }
                else if (!string.IsNullOrWhiteSpace(preferredPath))
                {
                        _selectedSessionPath = preferredPath;
                }
                else if (!string.IsNullOrWhiteSpace(_selectedSessionPath) && !_sessionHistory.Any(s => PathsEqual(s.FilePath, _selectedSessionPath)))
                {
                        _selectedSessionPath = _sessionHistory.FirstOrDefault()?.FilePath;
                }
                else if (string.IsNullOrWhiteSpace(_selectedSessionPath))
                {
                        _selectedSessionPath = _sessionHistory.FirstOrDefault()?.FilePath;
                }

                UpdateSessionNavigationAvailability();
        }

        private int GetSelectedSessionIndex()
        {
                if (string.IsNullOrWhiteSpace(_selectedSessionPath))
                        return -1;

                return _sessionHistory.FindIndex(s => PathsEqual(s.FilePath, _selectedSessionPath));
        }

        private SpeechSession? GetSelectedSession()
        {
                int index = GetSelectedSessionIndex();
                if (index < 0 || index >= _sessionHistory.Count)
                        return null;

                var session = _sessionHistory[index];
                if (!File.Exists(session.FilePath))
                {
                        RefreshSessions(preferredPath: _selectedSessionPath);
                        index = GetSelectedSessionIndex();
                        if (index < 0 || index >= _sessionHistory.Count)
                                return null;

                        session = _sessionHistory[index];
                        if (!File.Exists(session.FilePath))
                                return null;
                }

                return session;
        }

        private void UpdateSessionNavigationAvailability()
        {
                int index = GetSelectedSessionIndex();
                bool hasSessions = _sessionHistory.Count > 0;
                bool canMoveNewer = hasSessions && index > 0;
                bool canMoveOlder = hasSessions && index >= 0 && index < _sessionHistory.Count - 1;
                bool busy = _speechManager.Recording || _speechManager.Transcribing;

                BtnSessionNewer.IsEnabled = canMoveNewer && !busy;
                BtnSessionOlder.IsEnabled = canMoveOlder && !busy;

                string newerTooltip = canMoveNewer ? "Switch to a newer session" : "No newer sessions available";
                string olderTooltip = canMoveOlder ? "Switch to an older session" : "No older sessions available";
                ToolTipService.SetToolTip(BtnSessionNewer, newerTooltip);
                ToolTipService.SetToolTip(BtnSessionOlder, olderTooltip);
                AutomationProperties.SetHelpText(BtnSessionNewer, newerTooltip);
                AutomationProperties.SetHelpText(BtnSessionOlder, olderTooltip);
        }

        private void UpdateRecordingActionAvailability()
        {
                var session = GetSelectedSession();
                bool hasRecording = session != null && File.Exists(session.FilePath);
                bool busy = _speechManager.Recording || _speechManager.Transcribing;

                // The play button remains enabled during playback (_isPlayingRecording) so the user can stop playback,
                // but is disabled during other busy states (recording/transcribing) to prevent conflicts.
                BtnPlayLatestRecording.IsEnabled = ShouldEnablePlaySelectedSessionButton(hasRecording, busy);
                BtnRetrySpeechToText.IsEnabled = session != null && _activeSpeechService != null && !busy && !_isPlayingRecording;
                BtnUploadSpeechAudio.IsEnabled = !busy && !_isPlayingRecording;
                UpdateSessionNavigationAvailability();
        }

        /// <summary>
        /// Determines whether the Play Latest Recording button should be enabled.
        /// The button remains enabled during playback so the user can stop playback,
        /// but is disabled during other busy states (recording/transcribing) to prevent conflicts.
        /// </summary>
        private bool ShouldEnablePlaySelectedSessionButton(bool hasRecording, bool busy)
        {
                return _isPlayingRecording || (hasRecording && !busy);
        }

        private void ScheduleSessionCleanup()
        {
                var exclusions = new List<string>();
                var selected = GetSelectedSession();
                string? selectedPath = selected?.FilePath;
                if (!string.IsNullOrWhiteSpace(selectedPath))
                        exclusions.Add(selectedPath);

                if (_playingSession != null)
                        exclusions.Add(_playingSession.FilePath);

                var recording = _speechManager.CurrentRecordingSession;
                if (recording != null)
                        exclusions.Add(recording.FilePath);

                var cleanupTask = _speechManager.CleanupSessionsAsync(exclusions);
                cleanupTask.ContinueWith(_ =>
                {
                        RunOnDispatcher(() =>
                        {
                                RefreshSessions(preferredPath: selectedPath);
                                UpdateRecordingActionAvailability();
                        });
                }, TaskScheduler.Default);
        }

	private void FinalizeTranscript(string rawText, string successMessage)
	{
		string formatted = _transcriptFormatter.ApplyRules(rawText, false);

		TxtSpeechToText.Text = rawText;
		TxtFormatTranscript.Text = formatted;

		_clipboard.SetText(formatted);
		InsertIntoActiveApplication(formatted);

                BeepPlayer.Play(BeepType.Success);
                TxtSpeechToText.IsReadOnly = false;
                _suppressAutoActions = false;

                ShowStatus("Speech to Text", successMessage, InfoBarSeverity.Success);
                HotkeyManager.SendHotkeyAfterDelay(_settings.SpeechToTextSettings?.SendHotkeyAfterTranscriptionOperation, Constants.SendHotkeyDelay);
                UpdateRecordingActionAvailability();
                ScheduleSessionCleanup();
        }

	private void StopPlayback()
	{
		if (!_isPlayingRecording && _playbackPlayer.Source == null)
			return;

		_playbackPlayer.Pause();
		if (_playbackPlayer.PlaybackSession != null)
			_playbackPlayer.PlaybackSession.Position = TimeSpan.Zero;
                _playbackPlayer.Source = null;
                _playbackStream?.Dispose();
                _playbackStream = null;
                _isPlayingRecording = false;
                _playingSession = null;
                UpdatePlaybackButtonVisuals("Play selected session", PlayGlyph);
                UpdateRecordingActionAvailability();
        }

        private async Task<bool> StartPlaybackAsync(SpeechSession session)
        {
                if (session is null)
                        return false;

                if (!File.Exists(session.FilePath))
                {
                        RefreshSessions(preferredPath: _selectedSessionPath);
                        if (!File.Exists(session.FilePath))
                        {
                                ShowStatus("Speech to Text", "The selected session file is missing.", InfoBarSeverity.Warning);
                                UpdateRecordingActionAvailability();
                                return false;
                        }
                }

                StopPlayback();

                try
                {
                        byte[] bytes = File.ReadAllBytes(session.FilePath);
                        _playbackStream?.Dispose();
                        _playbackStream = new InMemoryRandomAccessStream();
                        using (var writer = new DataWriter(_playbackStream.GetOutputStreamAt(0)))
                        {
                                writer.WriteBytes(bytes);
                                await writer.StoreAsync();
                        }
                        _playbackStream.Seek(0);
                        var contentType = await DetermineContentTypeAsync(session.FilePath);
                        _playbackPlayer.Source = MediaSource.CreateFromStream(_playbackStream, contentType);
                }
                catch (Exception ex)
                {
                        _playbackStream?.Dispose();
                        _playbackStream = null;
                        ShowStatus("Speech to Text", $"Unable to play {session.FileName}: {ex.Message}", InfoBarSeverity.Error);
                        return false;
                }

                _playingSession = session;
                _isPlayingRecording = true;
                UpdatePlaybackButtonVisuals("Stop playback", StopGlyph);
                UpdateRecordingActionAvailability();
                ShowStatus("Speech to Text", $"Playing {session.FileName}...", InfoBarSeverity.Informational);
                _playbackPlayer.Play();
                return true;
        }

	private void PlaybackPlayer_MediaEnded(MediaPlayer sender, object args)
	{
		RunOnDispatcher(StopPlayback);
	}

	private void PlaybackPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
	{
		RunOnDispatcher(() => HandlePlaybackFailed(args.ErrorMessage));
	}

	private static string GetMimeTypeFromExtension(string path)
	{
		var extension = Path.GetExtension(path);
		if (!string.IsNullOrWhiteSpace(extension) && AudioMimeTypeMap.TryGetValue(extension, out var mimeType))
			return mimeType;

		return "audio/wav";
	}

	private static async Task<string> DetermineContentTypeAsync(string path)
	{
		try
		{
			var storageFile = await StorageFile.GetFileFromPathAsync(path);
			if (!string.IsNullOrWhiteSpace(storageFile.ContentType))
				return storageFile.ContentType;
		}
		catch (Exception)
		{
			// Swallow and fall back to extension-based detection.
		}

		return GetMimeTypeFromExtension(path);
	}

	private void RunOnDispatcher(DispatcherQueueHandler action)
	{
		if (!DispatcherQueue.TryEnqueue(action))
			action();
	}

        private void HandlePlaybackFailed(string errorMessage)
        {
                string sessionName = _playingSession?.FileName ?? "session";
                StopPlayback();
                ShowStatus("Speech to Text", $"Playback failed for {sessionName}: {errorMessage}", InfoBarSeverity.Error);
        }

	private void ConfigureButtonHotkey(Button button, TextBlock? hotkeyTextBlock, string? hotkey, string baseTooltip)
	{
		string tooltip = ComposeTooltip(baseTooltip, hotkey);
		ToolTipService.SetToolTip(button, tooltip);
		AutomationProperties.SetHelpText(button, tooltip);
		UpdateHotkeyText(hotkeyTextBlock, hotkey);
	}

	private static void UpdateHotkeyText(TextBlock? hotkeyTextBlock, string? hotkey)
	{
		if (hotkeyTextBlock == null)
			return;

		if (string.IsNullOrWhiteSpace(hotkey))
		{
			hotkeyTextBlock.Visibility = Visibility.Collapsed;
		}
		else
		{
			hotkeyTextBlock.Text = $"Hotkey: {hotkey}";
			hotkeyTextBlock.Visibility = Visibility.Visible;
		}
	}

        private static string ComposeTooltip(string baseTooltip, string? hotkey) =>
                          string.IsNullOrWhiteSpace(hotkey) ? baseTooltip : $"{baseTooltip} (Hotkey: {hotkey})";

        private static bool PathsEqual(string? left, string? right) =>
                string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private void ShowStatus(string title, string message, InfoBarSeverity severity)
        {
                void Update()
                {
			StatusInfoBar.Title = title;
			StatusInfoBar.Message = message;
			StatusInfoBar.Severity = severity;
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
	}

	private void StatusInfoBar_CloseButtonClick(InfoBar sender, object args)
	{
		_statusDismissTimer.Stop();
		StatusInfoBar.IsOpen = false;
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
				_settings.AudioSettings.ActiveCaptureDeviceFullName = GetDeviceFriendlyName(device);
				_settingsManager.SaveSettingsToFile(_settings);
			}
			RestartMicrophoneVisualizationCapture();
		}
		else
		{
			StopMicrophoneVisualizationCapture();
		}
	}

	private void MicWaveArea_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (_settings.AudioSettings == null)
			return;
		bool newState = !_settings.AudioSettings.EnableMicrophoneVisualization;
		_settings.AudioSettings.EnableMicrophoneVisualization = newState;
		_settingsManager.SaveSettingsToFile(_settings);
		if (newState)
		{
			InitializeMicrophoneVisualization();
			StartMicrophoneVisualizationCapture();
		}
		else
		{
			DisposeMicrophoneVisualization();
			if (MicWaveformOffLabel != null)
				MicWaveformOffLabel.Visibility = Visibility.Visible;
		}
	}

	private void CmbSpeechService_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (CmbSpeechService.SelectedItem is ISpeechToTextService svc)
			_activeSpeechService = svc;
		UpdateRecordingActionAvailability();
	}

	private void CmbInsertOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (CmbInsertOption.SelectedItem is DictationInsertOption opt)
		{
			_insertOption = opt;
			UpdateThirdPartyExplanation(opt);
		}
	}

	private void UpdateThirdPartyExplanation(DictationInsertOption option)
	{
		string explanation = option switch
		{
			DictationInsertOption.DoNotInsert => DoNotInsertExplanation,
			DictationInsertOption.SendKeys => SendKeysExplanation,
			DictationInsertOption.Paste => PasteExplanation,
			_ => string.Empty
		};

		ThirdPartyExplanationText.Text = explanation;
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

	private async void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
	{
		if (Content is not FrameworkElement rootElement)
		{
			return;
		}

		var settingsDialog = new SettingsDialog
		{
			XamlRoot = rootElement.XamlRoot,
			RequestedTheme = rootElement.ActualTheme
		};

		await settingsDialog.ShowAsync();
	}
}
