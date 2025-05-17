using CognitiveSupport;

namespace Mutation;

public class HotkeyManager
{
	private readonly Settings _settings;
	private readonly List<Hotkey> _routerHotkeys = new();

	private Control? _owner;

	private Hotkey? _hkToggleMicMute;
	private Hotkey? _hkSpeechToText;
	private Hotkey? _hkScreenshot;
	private Hotkey? _hkScreenshotOcr;
	private Hotkey? _hkScreenshotLeftToRightTopToBottomOcr;
	private Hotkey? _hkOcr;
	private Hotkey? _hkOcrLeftToRightTopToBottom;
	private Hotkey? _hkTextToSpeech;

	public Hotkey? ToggleMicMuteHotkey => _hkToggleMicMute;
	public Hotkey? SpeechToTextHotkey => _hkSpeechToText;
	public Hotkey? ScreenshotHotkey => _hkScreenshot;
	public Hotkey? ScreenshotOcrHotkey => _hkScreenshotOcr;
	public Hotkey? ScreenshotOcrLeftToRightTopToBottomHotkey => _hkScreenshotLeftToRightTopToBottomOcr;
	public Hotkey? OcrHotkey => _hkOcr;
	public Hotkey? OcrLeftToRightTopToBottomHotkey => _hkOcrLeftToRightTopToBottom;
	public Hotkey? TextToSpeechHotkey => _hkTextToSpeech;

	public HotkeyManager(Settings settings)
	{
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));
	}

	public void RegisterHotkeys(
		 Control owner,
		 Action screenshot,
		 Action<OcrReadingOrder> screenshotOcr,
		 Func<OcrReadingOrder, System.Threading.Tasks.Task> ocrFromClipboard,
		 Action toggleMicrophoneMute,
		 Func<System.Threading.Tasks.Task> speechToText,
		 Action textToSpeech)
	{
		_owner = owner ?? throw new ArgumentNullException(nameof(owner));

		if (_settings.AzureComputerVisionSettings == null)
			throw new InvalidOperationException("AzureComputerVisionSettings is not configured.");
		if (_settings.AudioSettings == null)
			throw new InvalidOperationException("AudioSettings is not configured.");
		if (_settings.SpeetchToTextSettings == null)
			throw new InvalidOperationException("SpeetchToTextSettings is not configured.");
		if (_settings.TextToSpeechSettings == null)
			throw new InvalidOperationException("TextToSpeechSettings is not configured.");

		_hkScreenshot = MapHotKey(_settings.AzureComputerVisionSettings.ScreenshotHotKey ?? throw new InvalidOperationException("ScreenshotHotKey is not set."));
		_hkScreenshot.Pressed += (s, e) => screenshot();
		TryRegisterHotkey(_hkScreenshot);

		_hkScreenshotOcr = MapHotKey(_settings.AzureComputerVisionSettings.ScreenshotOcrHotKey ?? throw new InvalidOperationException("ScreenshotOcrHotKey is not set."));
		_hkScreenshotOcr.Pressed += (s, e) => screenshotOcr(OcrReadingOrder.TopToBottomColumnAware);
		TryRegisterHotkey(_hkScreenshotOcr);

		_hkScreenshotLeftToRightTopToBottomOcr = MapHotKey(_settings.AzureComputerVisionSettings.ScreenshotLeftToRightTopToBottomOcrHotKey ?? throw new InvalidOperationException("ScreenshotLeftToRightTopToBottomOcrHotKey is not set."));
		_hkScreenshotLeftToRightTopToBottomOcr.Pressed += (s, e) => screenshotOcr(OcrReadingOrder.LeftToRightTopToBottom);
		TryRegisterHotkey(_hkScreenshotLeftToRightTopToBottomOcr);

		_hkOcr = MapHotKey(_settings.AzureComputerVisionSettings.OcrHotKey ?? throw new InvalidOperationException("OcrHotKey is not set."));
		_hkOcr.Pressed += async (s, e) => await ocrFromClipboard(OcrReadingOrder.TopToBottomColumnAware).ConfigureAwait(false);
		TryRegisterHotkey(_hkOcr);

		_hkOcrLeftToRightTopToBottom = MapHotKey(_settings.AzureComputerVisionSettings.OcrLeftToRightTopToBottomHotKey ?? throw new InvalidOperationException("OcrLeftToRightTopToBottomHotKey is not set."));
		_hkOcrLeftToRightTopToBottom.Pressed += async (s, e) => await ocrFromClipboard(OcrReadingOrder.LeftToRightTopToBottom).ConfigureAwait(false);
		TryRegisterHotkey(_hkOcrLeftToRightTopToBottom);

		_hkToggleMicMute = MapHotKey(_settings.AudioSettings.MicrophoneToggleMuteHotKey ?? throw new InvalidOperationException("MicrophoneToggleMuteHotKey is not set."));
		_hkToggleMicMute.Pressed += (s, e) => toggleMicrophoneMute();
		TryRegisterHotkey(_hkToggleMicMute);

		_hkSpeechToText = MapHotKey(_settings.SpeetchToTextSettings.SpeechToTextHotKey ?? throw new InvalidOperationException("SpeechToTextHotKey is not set."));
		_hkSpeechToText.Pressed += async (s, e) => await speechToText().ConfigureAwait(false);
		TryRegisterHotkey(_hkSpeechToText);

		_hkTextToSpeech = MapHotKey(_settings.TextToSpeechSettings.TextToSpeechHotKey ?? throw new InvalidOperationException("TextToSpeechHotKey is not set."));
		_hkTextToSpeech.Pressed += (s, e) => textToSpeech();
		TryRegisterHotkey(_hkTextToSpeech);

		RegisterRouterHotkeys();
	}

	private void RegisterRouterHotkeys()
	{
		foreach (var mapping in _settings.HotKeyRouterSettings.Mappings)
		{
			if (string.IsNullOrWhiteSpace(mapping.FromHotKey))
				throw new InvalidOperationException("Router mapping FromHotKey is not set.");
			if (string.IsNullOrWhiteSpace(mapping.ToHotKey))
				throw new InvalidOperationException("Router mapping ToHotKey is not set.");
			Hotkey fromHotKey = MapHotKey(mapping.FromHotKey);
			fromHotKey.Pressed += (s, e) => SendKeysAfterDelay(mapping.ToHotKey, 25);
			if (TryRegisterHotkey(fromHotKey))
				_routerHotkeys.Add(fromHotKey);
		}
	}

	public void UnregisterHotkeys()
	{
		UnregisterHotkey(_hkToggleMicMute);
		UnregisterHotkey(_hkSpeechToText);
		UnregisterHotkey(_hkScreenshot);
		UnregisterHotkey(_hkScreenshotOcr);
		UnregisterHotkey(_hkScreenshotLeftToRightTopToBottomOcr);
		UnregisterHotkey(_hkOcr);
		UnregisterHotkey(_hkOcrLeftToRightTopToBottom);
		UnregisterHotkey(_hkTextToSpeech);
		foreach (var hk in _routerHotkeys)
			UnregisterHotkey(hk);
	}

	public static void SendKeysAfterDelay(string hotkey, int delayMs)
	{
		if (string.IsNullOrWhiteSpace(hotkey))
			return;

		System.Threading.Tasks.Task.Run(async () =>
		{
			await System.Threading.Tasks.Task.Delay(delayMs);
			System.Windows.Forms.SendKeys.SendWait(hotkey);
		});
	}

	private bool TryRegisterHotkey(Hotkey hotKey)
	{
		if (_owner == null)
			throw new InvalidOperationException("Hotkeys not initialized");

		if (!hotKey.GetCanRegister(_owner))
		{
			if (_owner is Form f)
				f.Activate();
			MessageBox.Show($"Oops, looks like attempts to register the hotkey {hotKey} will fail or throw an exception.");
			return false;
		}
		else
		{
			hotKey.Register(_owner);
			return true;
		}
	}

	private static void UnregisterHotkey(Hotkey? hk)
	{
		if (hk != null && hk.Registered)
			hk.Unregister();
	}

	private static Hotkey MapHotKey(string hotKeyStringRepresentation)
	{
		var hotKey = new Hotkey();

		var keyStrings = hotKeyStringRepresentation.Split("_-+;, :".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
			 .Select(k => k.ToUpper())
			 .ToList();
		string mainKeyString = keyStrings.Last();
		mainKeyString = NormalizeKeyString(mainKeyString);
		hotKey.KeyCode = Enum.Parse<Keys>(mainKeyString, true);

		if (keyStrings.Contains("ALT"))
			hotKey.Alt = true;
		if (keyStrings.Contains("CTRL") || keyStrings.Contains("CONTROL"))
			hotKey.Control = true;
		if (keyStrings.Contains("SHFT") || keyStrings.Contains("SHIFT"))
			hotKey.Shift = true;
		if (keyStrings.Contains("WIN") || keyStrings.Contains("WINDOWS") || keyStrings.Contains("START"))
			hotKey.Windows = true;

		return hotKey;
	}

	private static string NormalizeKeyString(string keyString)
	{
		keyString = keyString.Replace("{", "").Replace("}", "");

		return keyString.ToLowerInvariant() switch
		{
			"del" => "delete",
			"ins" => "insert",
			_ => keyString
		};
	}
}
