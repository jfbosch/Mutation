using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using CognitiveSupport;

namespace Mutation
{
	public partial class MutationForm : Form
	{
		internal Hotkey _hkOcr;
		private OcrService OcrService { get; set; }


		internal Hotkey _hkToggleMicMute;
		private bool IsMuted = false;
		private CoreAudioController _audioController;
		IEnumerable<CoreAudioDevice> _devices;
		private CoreAudioDevice Microphone { get; set; }

		public MutationForm()
		{
			InitializeComponent();
			InitializeAudioControls();

			OcrService = new OcrService(null, null);

			HookupHotkeys();
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

			HookupOcrExtractText();

			lbl2.Text = "Toggle Michrophone Mute: " + _hkToggleMicMute;
		}

		private void HookupOcrExtractText()
		{
			_hkOcr = new Hotkey();
			_hkOcr.Control = true;
			_hkOcr.Shift = true;
			_hkOcr.KeyCode = Keys.Z;
			_hkOcr.Pressed += delegate { ToggleMicrophoneMute(); };

			//TryRegisterHotkey(_hkOcr);
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
