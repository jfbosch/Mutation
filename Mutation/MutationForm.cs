using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using CognitiveSupport;
using System.Drawing.Imaging;

namespace Mutation
{
	public partial class MutationForm : Form
	{
		private Settings Settings { get; set; }

		internal Hotkey _hkOcr;
		private OcrService OcrService { get; set; }


		internal Hotkey _hkToggleMicMute;
		private bool IsMuted = false;
		private CoreAudioController _audioController;
		IEnumerable<CoreAudioDevice> _devices;
		private CoreAudioDevice Microphone { get; set; }

		public MutationForm()
		{
			LoadSettings();

			InitializeComponent();
			InitializeAudioControls();

			OcrService = new OcrService(null, null);

			HookupHotkeys();
		}

		private void LoadSettings()
		{
			try
			{
				string filePath = "Mutation.json";
				this.Settings = new SettingsManager(filePath).LoadSettings();
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
			HookupToggleMichrophoneMuteHotkey();
			lbl2.Text = "Toggle Michrophone Mute: " + _hkToggleMicMute;

			HookupOcrExtractText();
		}

		private void HookupOcrExtractText()
		{
			_hkOcr = new Hotkey();
			_hkOcr.Alt = true;
			_hkOcr.KeyCode = Keys.J;
			_hkOcr.Pressed += delegate { ExtractText(); };

			TryRegisterHotkey(_hkOcr);
		}

		private async Task ExtractText()
		{
			try
			{
				Console.Beep(970, 80);

				Image image = GetClipboardImage();
				if (image is not null)
				{
					using MemoryStream imageStream = new MemoryStream();
					//using FileStream imageStream = new FileStream(@"C:\Temp\1.jpg", FileMode.OpenOrCreate, FileAccess.ReadWrite);
					image.Save(imageStream, ImageFormat.Jpeg);
					imageStream.Seek(0, SeekOrigin.Begin);
					string text = await this.OcrService.ExtractText(imageStream).ConfigureAwait(true);

					//MessageBox.Show(text, "OCR", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);

					SetTextToClipboard(text);
					Console.Beep(1050, 40);
					Console.Beep(1050, 40);

					//using MessageForm msgForm = new MessageForm();
					//msgForm.Show();
					//msgForm.Activate();


				}
				else
				{
					Console.Beep(550, 40);
					Console.Beep(550, 40);

					this.Activate();
					MessageBox.Show("No image found on the clipboard.");
				}
			}
			catch (Exception ex)
			{
				this.Activate();
				MessageBox.Show($"Failed extract text via OCR: {ex.Message}{Environment.NewLine}{ex.GetType().FullName}{Environment.NewLine}{ex.StackTrace}");
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

		private void HookupToggleMichrophoneMuteHotkey()
		{
			_hkToggleMicMute = new Hotkey();
			_hkToggleMicMute.Alt = true;
			_hkToggleMicMute.KeyCode = Keys.Q;
			_hkToggleMicMute.Pressed += delegate { ToggleMicrophoneMute(); };

			TryRegisterHotkey(_hkToggleMicMute);
		}

		private void TryRegisterHotkey(Hotkey hotkey)
		{
			if (!hotkey.GetCanRegister(this))
				MessageBox.Show("Whoops, looks like attempts to register the hotkey " + hotkey + " will fail or throw an exception.");
			else
				hotkey.Register(this);
		}

		private void MutationForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (_hkToggleMicMute != null && _hkToggleMicMute.Registered)
				_hkToggleMicMute.Unregister();
		}
	}
}
