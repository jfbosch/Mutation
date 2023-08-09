using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using CognitiveSupport;
using System.Drawing.Imaging;

namespace Mutation
{
	public partial class MutationForm : Form
	{
		private Settings Settings { get; set; }

		private Hotkey _hkOcr;
		private OcrService OcrService { get; set; }

		private SemaphoreSlim _audioRecorderLock = new SemaphoreSlim(1, 1);
		private AudioRecorder AudioRecorder { get; set; }
		private bool RecordingAudio => AudioRecorder != null;

		private SpeechToTextService SpeechToTextService { get; set; }
		private Hotkey _hkSpeechToText { get; set; }


		private Hotkey _hkToggleMicMute;
		private bool IsMuted = false;
		private CoreAudioController _audioController;
		private IEnumerable<CoreAudioDevice> _devices;
		private CoreAudioDevice Microphone { get; set; }

		public MutationForm()
		{
			LoadSettings();

			InitializeComponent();
			InitializeAudioControls();

			OcrService = new OcrService(Settings.AzureComputerVisionSettings.SubscriptionKey, Settings.AzureComputerVisionSettings.Endpoint);
			SpeechToTextService = new SpeechToTextService(
				Settings.OpenAiSettings.ApiKey
				, Settings.OpenAiSettings.Endpoint);

			HookupHotkeys();
		}

		private void LoadSettings()
		{
			try
			{
				string filePath = "Mutation.json";
				this.Settings = new SettingsManager(filePath).LoadAndEnsureSettings();
			}
			catch (Exception ex)
				when (ex.Message.ToLower().Contains("could not find the settings"))
			{
				MessageBox.Show($"Failed to load settings: {ex.Message}");
			}

		}

		internal void InitializeAudioControls()
		{
			txtActiveMic.Text = "(Initializing...)";
			Application.DoEvents();

			_audioController = new CoreAudioController();
			_devices = _audioController.GetDevices(DeviceType.Capture, DeviceState.Active);
			var defaultMicDevice = _devices
				.FirstOrDefault(x => x.IsDefaultDevice);
			if (defaultMicDevice != null)
			{
				this.Microphone = defaultMicDevice;

				FeedbackToUser();
			}
			else
			{
				txtActiveMic.Text = "(Unable to find device)";
				Console.Beep(300, 100);
				Console.Beep(300, 100);
				Console.Beep(300, 100);
			}
		}

		public void ToggleMicrophoneMute()
		{
			lock (this)
			{
				IsMuted = !IsMuted;
				foreach (var mic in _devices)
					mic.Mute(IsMuted);

				FeedbackToUser();
			}
		}

		private void FeedbackToUser()
		{
			lock (this)
			{
				if (Microphone.IsMuted)
				{
					this.Text = "Muted Microphone";
					this.BackColor = Color.LightGray;
					Console.Beep(500, 200);
				}
				else
				{
					this.Text = "Unuted Microphone";
					this.BackColor = Color.White;
					Console.Beep(1300, 50);
				}

				txtActiveMic.Text = this.Microphone.Name;

				int i = 1;
				txtAllMics.Text = string.Join(Environment.NewLine, _devices.Select(m => $"{i++}) {m.FullName}{(m.IsMuted ? "       - muted" : "")}").ToArray());

			}
		}

		private void HookupHotkeys()
		{
			HookupHotKeyToggleMichrophoneMuteHotkey();
			HookupHotKeyOcrExtractText();
			HookupHotKeySpeechToText();
		}

		private void HookupHotKeyOcrExtractText()
		{
			_hkOcr = MapHotKey(Settings.AzureComputerVisionSettings.OcrImageToTextHotKey);
			_hkOcr.Pressed += delegate { ExtractText(); };
			TryRegisterHotkey(_hkOcr);

			lblOcrHotKey.Text = $"OCR Clipboard: {_hkOcr}";
		}

		private async Task ExtractText()
		{
			try
			{
				Console.Beep(970, 80);

				txtOcr.Text = "Running OCR on image";

				Image image = GetClipboardImage();
				if (image is not null)
				{
					using MemoryStream imageStream = new MemoryStream();
					image.Save(imageStream, ImageFormat.Jpeg);
					imageStream.Seek(0, SeekOrigin.Begin);
					string text = await this.OcrService.ExtractText(imageStream).ConfigureAwait(true);

					//MessageBox.Show(text, "OCR", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);

					SetTextToClipboard(text);
					txtOcr.Text = $"Converted text is on clipboard:{Environment.NewLine}{text}";

					Console.Beep(1050, 40);
					Console.Beep(1050, 40);

					//using MessageForm msgForm = new MessageForm();
					//msgForm.Show();
					//msgForm.Activate();
				}
				else
				{
					Console.Beep(550, 40);
					Console.Beep(400, 40);

					txtOcr.Text = "No image found on the clipboard.";
					this.Activate();
					MessageBox.Show("No image found on the clipboard.");
				}
			}
			catch (Exception ex)
			{
				string msg = $"Failed to extract text via OCR: {ex.Message}{Environment.NewLine}{ex.GetType().FullName}{Environment.NewLine}{ex.StackTrace}";
				txtOcr.Text = msg;

				Console.Beep(550, 80);
				Console.Beep(400, 80);

				//this.Activate();
				//MessageBox.Show(msg);
			}
		}

		public Image GetClipboardImage()
		{
			Image returnImage = null;
			if (Clipboard.ContainsImage())
			{
				returnImage = Clipboard.GetImage();
			}
			return returnImage;
		}

		// https://docs.microsoft.com/en-us/dotnet/desktop/winforms/advanced/how-to-retrieve-data-from-the-clipboard?view=netframeworkdesktop-4.8
		public void SetTextToClipboard(string text)
		{
			Clipboard.SetText(text, TextDataFormat.Text);
		}


		private void HookupHotKeyToggleMichrophoneMuteHotkey()
		{
			_hkToggleMicMute = MapHotKey(Settings.AudioSettings.MicrophoneToggleMuteHotKey);
			_hkToggleMicMute.Pressed += delegate { ToggleMicrophoneMute(); };
			TryRegisterHotkey(_hkToggleMicMute);

			lblToggleMic.Text = $"Toggle Michrophone Mute: {_hkToggleMicMute}";
		}

		private void HookupHotKeySpeechToText()
		{
			_hkSpeechToText = MapHotKey(Settings.OpenAiSettings.SpeechToTextHotKey);
			_hkSpeechToText.Pressed += delegate { SpeechToText(); };
			TryRegisterHotkey(_hkSpeechToText);

			lblSpeechToText.Text = $"Speach to Text: {_hkSpeechToText}";
		}

		private async Task SpeechToText()
		{
			try
			{
				if (!Directory.Exists(Settings.OpenAiSettings.TempDirectory))
					Directory.CreateDirectory(Settings.OpenAiSettings.TempDirectory);

				string audioFilePath = Path.Combine(Settings.OpenAiSettings.TempDirectory, "mutation_recording.mp3");

				await _audioRecorderLock.WaitAsync().ConfigureAwait(true);
				{
					if (!RecordingAudio)
					{
						txtSpeechToText.Text = "Recording microphone...";
						AudioRecorder = new AudioRecorder();
						AudioRecorder.StartRecording(audioFilePath);
						Console.Beep(970, 80);
					}
					else // Busy recording, so we want to stop it.
					{
						AudioRecorder.StopRecording();
						AudioRecorder.Dispose();
						AudioRecorder = null;

						Console.Beep(1050, 40);

						txtSpeechToText.Text = "Converting speech to text...";
						string text = await this.SpeechToTextService.ConvertAudioToText(audioFilePath).ConfigureAwait(true);

						SetTextToClipboard(text);
						txtSpeechToText.Text = $"Converted text is on clipboard:{Environment.NewLine}{text}";

						Console.Beep(1050, 40);
						Console.Beep(1150, 40);
					}
				}

			}
			catch (Exception ex)
			{
				Console.Beep(550, 40);
				Console.Beep(550, 40);

				string msg = $"Failed speech to text: {ex.Message}{Environment.NewLine}{ex.GetType().FullName}{Environment.NewLine}{ex.StackTrace}"; ;
				txtSpeechToText.Text = msg;

				this.Activate();
				MessageBox.Show(msg);
			}
			finally
			{
				_audioRecorderLock.Release();
			}
		}


		private static Hotkey MapHotKey(string hotKeyStringRepresentation)
		{
			var hotKey = new Hotkey();

			var keyStrings = hotKeyStringRepresentation.Split(@"_-+,;: ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
				.Select(k => k.ToUpper())
				.ToList();
			string mainKeyString = keyStrings.Last();
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

		private void TryRegisterHotkey(Hotkey hotKey)
		{
			if (!hotKey.GetCanRegister(this))
			{
				this.Activate();
				MessageBox.Show($"Oops, looks like attempts to register the hotkey {hotKey} will fail or throw an exception.");
			}
			else
				hotKey.Register(this);
		}

		private void MutationForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			UnregisterHotkey(_hkToggleMicMute);
			UnregisterHotkey(_hkOcr);
		}

		private static void UnregisterHotkey(Hotkey hk)
		{
			if (hk != null && hk.Registered)
				hk.Unregister();
		}

		private void MutationForm_Load(object sender, EventArgs e)
		{

		}
	}
}
