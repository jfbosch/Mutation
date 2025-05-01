using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using CognitiveSupport;
using CognitiveSupport.Extensions;
using NAudio.Wave;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using ScreenCapturing;
using StringExtensionLibrary;
using System.ComponentModel;
using System.Drawing.Imaging;

namespace Mutation
{
	public partial class MutationForm : Form
	{
		private ScreenCaptureForm _activeScreenCaptureForm = null;
		private SpeechToTextServiceComboItem _activeSpeetchToTextServiceComboItem = null;

		private Settings _settings { get; set; }
		private ISettingsManager _settingsManager { get; set; }

		private Hotkey _hkToggleMicMute;
		private bool _isMuted = false;
		private CoreAudioController _coreAudioController;
		private IEnumerable<CoreAudioDevice> _captureDevices;
		private CoreAudioDevice _microphone { get; set; }
		private int _microphoneDeviceIndex = -1;

		private Hotkey _hkSpeechToText { get; set; }
		private ISpeechToTextService[] _speechToTextServices { get; set; }
		private AudioRecorder _audioRecorder { get; set; }
		private SpeechToTextState _speechToTextState { get; init; }

		private Hotkey _hkScreenshot;
		private Hotkey _hkScreenshotOcr;
		private Hotkey _hkOcr;
		private IOcrService _ocrService { get; set; }
		private OcrState _ocrState { get; init; } = new();

		private ILlmService _llmService { get; set; }

		private Hotkey _hkTextToSpeech { get; set; }
		private ITextToSpeechService _textToSpeechService;

		private List<Hotkey> HotKeyRouterFromEntries { get; set; } = new();

		public MutationForm(
			ISettingsManager settingsManager,
			Settings settings,
			CoreAudioController coreAudioController,
			IOcrService ocrService,
			ISpeechToTextService[] speechToTextServices,
			ITextToSpeechService textToSpeechService,
			ILlmService llmService)
		{
			this._settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
			this._settings = settings ?? throw new ArgumentNullException(nameof(settings));
			this._coreAudioController = coreAudioController ?? throw new ArgumentNullException(nameof(coreAudioController));
			this._ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
			this._speechToTextServices = speechToTextServices ?? throw new ArgumentNullException(nameof(speechToTextServices));
			this._textToSpeechService = textToSpeechService ?? throw new ArgumentNullException(nameof(textToSpeechService));
			this._llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
			this._speechToTextState = new SpeechToTextState(() => _audioRecorder);


			InitializeComponent();
			InitializeAudioControls();

			PopulateSpeechToTextServiceCombo();

			HookupTooltips();

			HookupHotkeys();

			txtFormatTranscriptPrompt.Text = this._settings.LlmSettings.FormatTranscriptPrompt;
			txtReviewTranscriptPrompt.Text = this._settings.LlmSettings.ReviewTranscriptPrompt;

			InitializeLlmReviewListView();

			cmbInsertInto3rdPartyApplication.DropDownStyle = ComboBoxStyle.DropDownList;
			foreach (DictationInsertOption option in Enum.GetValues(typeof(DictationInsertOption)))
			{
				string description = GetEnumDescription(option);
				cmbInsertInto3rdPartyApplication.Items.Add(new { Text = description, Value = option });
			}
			cmbInsertInto3rdPartyApplication.DisplayMember = "Text";
			cmbInsertInto3rdPartyApplication.ValueMember = "Value";
			cmbInsertInto3rdPartyApplication.SelectedIndex = 2;


			cmbReviewTemperature.DropDownStyle = ComboBoxStyle.DropDownList;
			for (decimal d = 0.0m; d < 1.9m; d = d + 0.1m)
			{
				cmbReviewTemperature.Items.Add(new { Text = $"{d}", Value = d });
			}
			cmbReviewTemperature.DisplayMember = "Text";
			cmbReviewTemperature.ValueMember = "Value";
			cmbReviewTemperature.SelectedIndex = 4;


			//BookMark??999

		}

		public static string GetEnumDescription(Enum value)
		{
			var fieldInfo = value.GetType().GetField(value.ToString());
			var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);

			if (attributes != null && attributes.Length > 0)
			{
				return attributes[0].Description;
			}
			else
			{
				return value.ToString();
			}
		}



		private void InitializeLlmReviewListView()
		{
			txtTranscriptReviewResponse.Visible = false;

			dgvReview.Location = txtTranscriptReviewResponse.Location;
			dgvReview.Height = txtTranscriptReviewResponse.Height;
			dgvReview.Width = txtTranscriptReviewResponse.Width;
			dgvReview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
			dgvReview.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
			dgvReview.AutoGenerateColumns = false;
			dgvReview.RowHeadersVisible = false;
			dgvReview.MultiSelect = false;
			dgvReview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

			DataGridViewCheckBoxColumn checkBoxColumn = new DataGridViewCheckBoxColumn();
			checkBoxColumn.HeaderText = "Select";
			checkBoxColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader;
			dgvReview.Columns.Add(checkBoxColumn);

			DataGridViewTextBoxColumn textColumn = new DataGridViewTextBoxColumn();
			textColumn.HeaderText = "Issue";
			textColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
			textColumn.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
			dgvReview.Columns.Add(textColumn);

			// Add sample data during dev time
			//dgvReview.Rows.Add(new object[] { false, "Long text item 1" });  // Adding an unchecked row
			//dgvReview.Rows.Add(new object[] { false, "Long text item 2...............................bla bla" });
			//dgvReview.Rows.Add(new object[] { false, "By setting the Anchor property to AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, the DataGridView will be anchored to all four sides of its parent container. This means that it will resize itself appropriately when the parent container is resized, maintaining the specified distance to each edge." });
		}

		private void HookupTooltips()
		{
			string speechToTextPromptToolTipMsg = @"
You can use a prompt to improve the quality of the transcripts generated by the Whisper API. The model will try to match the style of the prompt, so it will be more likely to use capitalization and punctuation if the prompt does too. This only provides limited control over the generated audio. Here are some examples of how prompting can help in different scenarios:

Prompts can be very helpful for correcting specific words or acronyms that the model often misrecognizes in the audio. For example, the following prompt improves the transcription of the words DALL·E and GPT-3, which were previously written as ""DALI"" and ""GDP 3"": The prompt is:
 “OpenAI makes technology like DALL·E, GPT-3, and ChatGPT with the hope of one day building an AGI system that benefits all of humanity""

Sometimes the model might skip punctuation in the transcript. You can avoid this by using a simple prompt that includes punctuation, such as: ""Hello, welcome to my lecture.""

The model may also leave out common filler words in the audio. If you want to keep the filler words in your transcript, you can use a prompt that contains them: ""Umm, let me think like, hmm... Okay, here's what I'serviceSettings, like, thinking.""
";

			toolTip.SetToolTip(txtSpeechToTextPrompt, speechToTextPromptToolTipMsg);
			toolTip.SetToolTip(lblSpeechToTextPrompt, speechToTextPromptToolTipMsg);

			var voiceCommands = this._settings.LlmSettings.TranscriptFormatRules
				.Select(x => new
				{
					x.Find,
					ReplaceWith = x.ReplaceWith
							.Replace(Environment.NewLine, @"<new line>")
							.Replace(@"\t", @"<tab>"),
					x.MatchType,
					x.CaseSensitive
				})
				.Select(x => new
				{
					Rule = x,
					Spacing = string.Concat(Enumerable.Repeat(
						" ",
						Math.Abs(75 - $"{x.Find} = {x.ReplaceWith}".Length)))
				})
				.Select(x => $"{x.Rule.Find} = {x.Rule.ReplaceWith}{x.Spacing}(Match: {x.Rule.MatchType}, Case Sensitive: {x.Rule.CaseSensitive})")
				.ToArray();
			string formattingCommandsPromptToolTipMsg = $"You can use the following voice commands while dictating: {Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, voiceCommands)}";
			toolTip.SetToolTip(lblSpeechToText, formattingCommandsPromptToolTipMsg);
			toolTip.SetToolTip(lblFormatTranscriptResponse, formattingCommandsPromptToolTipMsg);
		}

		private void RestoreWindowLocationAndSizeFromSettings()
		{
			if (_settings is null)
				return;

			if (_settings.MainWindowUiSettings.WindowSize != Size.Empty)
			{
				// Make sure the window size stays within the screen bounds
				this.Size = new Size(Math.Min(_settings.MainWindowUiSettings.WindowSize.Width, Screen.PrimaryScreen.Bounds.Width),
											Math.Min(_settings.MainWindowUiSettings.WindowSize.Height, Screen.PrimaryScreen.Bounds.Height));
			}

			if (this.Size.Width < 150 || this.Size.Height < 150)
			{
				this.Size = new Size(Math.Max(this.Size.Width, 150), Math.Max(this.Size.Height, 150));
			}

			if (_settings.MainWindowUiSettings.WindowLocation != Point.Empty)
			{
				// Make sure the window location stays within the screen bounds
				this.Location = new Point(Math.Max(Math.Min(_settings.MainWindowUiSettings.WindowLocation.X, Screen.PrimaryScreen.Bounds.Width - this.Size.Width), 0),
												  Math.Max(Math.Min(_settings.MainWindowUiSettings.WindowLocation.Y, Screen.PrimaryScreen.Bounds.Height - this.Size.Height), 0));

			}
		}

		internal void InitializeAudioControls()
		{
			txtActiveMicrophoneMuteState.Text = "(Initializing...)";

			Application.DoEvents();

			_captureDevices = _coreAudioController.GetDevices(DeviceType.Capture, DeviceState.Active);
			PopulateActiveMicrophoneCombo();
			SetActiveMicrophoneFromSettings();
			SetActiveMicrophoneToDefaultCaptureDeviceIfNotSet();
		}

		private void SetActiveMicrophoneToDefaultCaptureDeviceIfNotSet()
		{
			if (_microphone is null)
			{
				var defaultMicDevice = _captureDevices
					.FirstOrDefault(x => x.IsDefaultDevice);
				if (defaultMicDevice is not null)
				{
					this._microphone = defaultMicDevice;
					SelectCaptureDeviceForNAudioBasedRecording();
					SelectActiveCaptureDeviceInActiveMicrophoneCombo();

					FeedbackMicrophoneStateToUser();
				}
				else
				{
					txtActiveMicrophoneMuteState.Text = "(Unable to find device)";
					BeepFail();
				}
			}
		}

		private void SetActiveMicrophoneFromSettings()
		{
			foreach (CaptureDeviceComboItem item in cmbActiveMicrophone.Items)
			{
				if (item.CaptureDevice.FullName == _settings.AudioSettings.ActiveCaptureDeviceFullName)
				{
					cmbActiveMicrophone.SelectedItem = item;
					break;
				}
			}
		}

		private void PopulateActiveMicrophoneCombo()
		{
			cmbActiveMicrophone.Items.Clear();
			_captureDevices
				.ToList()
				.ForEach(m => cmbActiveMicrophone.Items.Add(new CaptureDeviceComboItem
				{
					CaptureDevice = m,
				}));
		}

		private void PopulateSpeechToTextServiceCombo()
		{
			cmbSpeechToTextService.Items.Clear();
			_settings.SpeetchToTextSettings.Services
				.ToList()
				.ForEach(serviceSettings => cmbSpeechToTextService.Items.Add(new SpeechToTextServiceComboItem
				{
					SpeetchToTextServiceSettings = serviceSettings,
					SpeechToTextService = _speechToTextServices.Single(x => x.ServiceName == serviceSettings.Name),
				}));

			SelectActiveServiceInSpeechToTextServiceCombo();
		}

		private void SelectActiveServiceInSpeechToTextServiceCombo()
		{
			foreach (SpeechToTextServiceComboItem item in cmbSpeechToTextService.Items)
			{
				if (item.SpeetchToTextServiceSettings.Name == _settings.SpeetchToTextSettings.ActiveSpeetchToTextService)
				{
					cmbSpeechToTextService.SelectedItem = item;
					break;
				}
			}
		}

		private void SelectActiveCaptureDeviceInActiveMicrophoneCombo()
		{
			foreach (CaptureDeviceComboItem item in cmbActiveMicrophone.Items)
			{
				if (item.CaptureDevice.FullName == _microphone.FullName)
				{
					cmbActiveMicrophone.SelectedItem = item;
					break;
				}
			}
		}

		private void SelectCaptureDeviceForNAudioBasedRecording()
		{
			// The AudioSwitcher library, CoreAudioDevice.Name returns a value like
			// "Krisp Michrophone". This is the name of the device as under Windows recording devices.
			// While the NAudio library(used for recording to file) property, WaveInEvent.GetCapabilities(i).ProductName, returns a value like
			// "Krisp Michrophone (Krisp Audio)". This has the device name, but also contains a suffix.
			// So, we do a starts with match to find the mic we are looking for using the default device name followed by a space and a (

			string startsWithNameToMatch = $"{this._microphone.Name} (";
			int deviceCount = WaveIn.DeviceCount;
			bool micMatchFound = false;
			for (int i = 0; i < deviceCount; i++)
			{
				if (WaveInEvent.GetCapabilities(i).ProductName.StartsWith(startsWithNameToMatch))
				{
					micMatchFound = true;
					_microphoneDeviceIndex = i;

					// Debugging message
					//MessageBox.Show(
					//	defaultMicDevice.Name
					//	+ Environment.NewLine
					//	+ WaveInEvent.GetCapabilities(i).ProductName
					//	+ Environment.NewLine
					//	+ "Device Index: " + _microphoneDeviceIndex);

					break;
				}
			}
			if (!micMatchFound)
				MessageBox.Show($"No michrophone match found for {this._microphone.Name}");
		}

		public void ToggleMicrophoneMute()
		{
			lock (this)
			{
				_isMuted = !_isMuted;
				foreach (var mic in _captureDevices)
					mic.Mute(_isMuted);

				FeedbackMicrophoneStateToUser();
			}
		}

		private void FeedbackMicrophoneStateToUser()
		{
			lock (this)
			{
				if (_microphone.IsMuted)
				{
					this.Text = "Mutation - Muted Microphone";
					this.BackColor = Color.LightGray;
					BeepMuted();
				}
				else // unmuted
				{
					this.Text = "Mutation - Unmuted Microphone";
					this.BackColor = Color.WhiteSmoke;
					BeepUnmuted();
				}

				txtActiveMicrophoneMuteState.Text = this._microphone.IsMuted ? "Muted" : "Unmuted";

				int i = 1;
				txtAllMics.Text = string.Join(Environment.NewLine, _captureDevices.Select(m => $"{i++}) {m.FullName}{(m.IsMuted ? "       - muted" : "")}").ToArray());
			}
		}

		private void HookupHotkeys()
		{
			HookupHotKeyToggleMichrophoneMuteHotkey();

			HookupHotKeyScreenshot();
			HookupHotKeyScreenshotOcr();
			HookupHotKeyOcr();

			HookupHotKeySpeechToText();
			HookupHotKeyTextToSpeech();

			HookupHotKeyRouter();
		}

		private void HookupHotKeyScreenshot()
		{
			_hkScreenshot = MapHotKey(_settings.AzureComputerVisionSettings.ScreenshotHotKey);
			_hkScreenshot.Pressed += delegate { TakeScreenshotToClipboard(); };
			TryRegisterHotkey(_hkScreenshot);

			lblScreenshotHotKey.Text = $"Screenshot: {_hkScreenshot}";
		}

		private void TakeScreenshotToClipboard()
		{
			if (_activeScreenCaptureForm is not null)
			{
				// If there is already an active capture form open, just make sure it is topmost and short-circuit.
				_activeScreenCaptureForm?.Activate();
				return;
			}

			using (Bitmap screenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
			using (Graphics g = Graphics.FromImage(screenshot))
			{
				g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);

				var displayShot = screenshot;
				using Bitmap invertedScreenshot = InvertScreenshotColors(screenshot);
				if (_settings.AzureComputerVisionSettings.InvertScreenshot)
					displayShot = invertedScreenshot;

				using ScreenCaptureForm screenCaptureForm = new ScreenCaptureForm(new Bitmap(displayShot));

				_activeScreenCaptureForm = screenCaptureForm;

				screenCaptureForm.TopMost = true;
				screenCaptureForm.ShowDialog();

				_activeScreenCaptureForm = null;

			}
		}

		private Bitmap InvertScreenshotColors(Bitmap original)
		{
			Bitmap inverted = new Bitmap(original.Width, original.Height);
			using (Graphics g = Graphics.FromImage(inverted))
			{
				// Define a color matrix that inverts the RGB values.
				ColorMatrix invertMatrix = new ColorMatrix(new float[][]
				{
				new float[]{ -1,  0,  0, 0, 0 },
				new float[]{  0, -1,  0, 0, 0 },
				new float[]{  0,  0, -1, 0, 0 },
				new float[]{  0,  0,  0, 1, 0 },
				new float[]{  1,  1,  1, 0, 1 }
				});

				using (ImageAttributes attributes = new ImageAttributes())
				{
					attributes.SetColorMatrix(invertMatrix);
					g.DrawImage(original,
						 new Rectangle(0, 0, original.Width, original.Height),
						 0, 0, original.Width, original.Height,
						 GraphicsUnit.Pixel, attributes);
				}
			}
			return inverted;
		}

		private void HookupHotKeyScreenshotOcr()
		{
			_hkScreenshotOcr = MapHotKey(_settings.AzureComputerVisionSettings.ScreenshotOcrHotKey);
			_hkScreenshotOcr.Pressed += delegate { TakeScreenshotAndExtractText(); };
			TryRegisterHotkey(_hkScreenshotOcr);

			lblScreenshotOcrHotKey.Text = $"Screenshot OCR: {_hkScreenshotOcr}";
		}

		private async void TakeScreenshotAndExtractText()
		{
			if (_activeScreenCaptureForm is not null)
			{
				// If there is already an active capture form open, just make sure it is topmost and short-circuit.
				_activeScreenCaptureForm?.Activate();
				return;
			}

			using (Bitmap screenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
			using (Graphics g = Graphics.FromImage(screenshot))
			{
				g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);

				var displayShot = screenshot;
				using Bitmap invertedScreenshot = InvertScreenshotColors(screenshot);
				if (_settings.AzureComputerVisionSettings.InvertScreenshot)
					displayShot = invertedScreenshot;

				using (ScreenCaptureForm screenCaptureForm = new ScreenCaptureForm(new Bitmap(displayShot)))
				{
					_activeScreenCaptureForm = screenCaptureForm;

					screenCaptureForm.TopMost = true;
					screenCaptureForm.ShowDialog();

					_activeScreenCaptureForm = null;

					await ExtractTextViaOcrFromClipboardImage();
				}
			}
		}

		private void HookupHotKeyOcr()
		{
			_hkOcr = MapHotKey(_settings.AzureComputerVisionSettings.OcrHotKey);
			_hkOcr.Pressed += delegate
			{
				ExtractTextViaOcrFromClipboardImage();
			};
			TryRegisterHotkey(_hkOcr);

			lblOcrHotKey.Text = $"OCR Clipboard: {_hkOcr}";
		}

		private async Task ExtractTextViaOcrFromClipboardImage()
		{
			if (_ocrState.BusyWithTextExtraction)
			{
				_ocrState.StopTextExtraction();
				return;
			}

			var image = TryGetClipboardImage();
			if (image is null)
			{
				var msg = "No image found on the clipboard after multiple retries.";
				txtOcr.Text = msg;
				SetTextToClipboard(msg);
				BeepFail();
				return;
			}

			try
			{
				_ocrState.StartTextExtraction();
				await ExtractTextViaOcr(TryGetClipboardImage());
			}
			finally
			{
				_ocrState.StopTextExtraction();
			}
		}

		private async Task ExtractTextViaOcr(
			Image image)
		{
			if (image is null)
			{
				txtOcr.Text = "No image provided to perform OCR on.";
				return;
			}

			try
			{
				BeepStart();

				txtOcr.Text = "Running OCR on image";

				using MemoryStream imageStream = new MemoryStream();
				image.Save(imageStream, ImageFormat.Jpeg);
				imageStream.Seek(0, SeekOrigin.Begin);
				string text = await this._ocrService.ExtractText(OcrReadingOrder.TopToBottomColumnAware, imageStream, _ocrState.OcrCancellationTokenSource.Token).ConfigureAwait(true);

				SetTextToClipboard(text);
				txtOcr.Text = $"Converted text is on clipboard:{Environment.NewLine}{text}";

				BeepSuccess();
			}
			catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				// This was an intentional cancellation by the user, so only beep the failure, but don't show an error message. 
				BeepFail();

				txtOcr.Text = "OCR cancelled by user.";
				SetTextToClipboard(txtOcr.Text);
			}
			catch (Exception ex)
			{
				string msg = $"Failed to extract text via OCR: {ex.Message}{Environment.NewLine}{ex.GetType().FullName}{Environment.NewLine}{ex.StackTrace}";
				txtOcr.Text = msg;

				BeepFail();
				SetTextToClipboard(msg);
			}
		}

		public Image TryGetClipboardImage()
		{
			int attempts = 5;

			while (attempts > 0)
			{
				if (Clipboard.ContainsImage())
				{
					return Clipboard.GetImage();
				}

				attempts--;
				Thread.Sleep(150);
			}

			return null;
		}

		// https://docs.microsoft.com/en-us/dotnet/desktop/winforms/advanced/how-to-retrieve-data-from-the-clipboard?view=netframeworkdesktop-4.8
		public void SetTextToClipboard(
			string text)
		{
			if (!string.IsNullOrWhiteSpace(text))
				Clipboard.SetText(text, TextDataFormat.UnicodeText);
		}

		private void HookupHotKeyToggleMichrophoneMuteHotkey()
		{
			_hkToggleMicMute = MapHotKey(_settings.AudioSettings.MicrophoneToggleMuteHotKey);
			_hkToggleMicMute.Pressed += delegate { ToggleMicrophoneMute(); };
			TryRegisterHotkey(_hkToggleMicMute);

			lblToggleMic.Text = $"Toggle Michrophone Mute: {_hkToggleMicMute}";
		}

		private void HookupHotKeySpeechToText()
		{
			_hkSpeechToText = MapHotKey(_settings.SpeetchToTextSettings.SpeechToTextHotKey);
			_hkSpeechToText.Pressed += delegate { SpeechToText(); };
			TryRegisterHotkey(_hkSpeechToText);

			lblSpeechToText.Text = $"Speach to Text: {_hkSpeechToText}";
		}

		private void HookupHotKeyTextToSpeech()
		{
			_hkTextToSpeech = MapHotKey(_settings.TextToSpeechSettings.TextToSpeechHotKey);
			_hkTextToSpeech.Pressed += delegate { TextToSpeech(); };
			TryRegisterHotkey(_hkTextToSpeech);

			//lblTextToSpeech.Text = $"Text to Speech: {_hkTextToSpeech}";
		}

		private void HookupHotKeyRouter()
		{
			foreach (var mapping in _settings.HotKeyRouterSettings.Mappings)
			{
				Hotkey fromHotKey = MapHotKey(mapping.FromHotKey);
				fromHotKey.Pressed += delegate { SendKeysAfterDelay(mapping.ToHotKey, 25); };
				if (TryRegisterHotkey(fromHotKey))
					this.HotKeyRouterFromEntries.Add(fromHotKey);
			}
		}

		private static void SendKeysAfterDelay(
			string hotkey,
			int delayMs)
		{
			System.Threading.Tasks.Task.Run(async () =>
			{
				await System.Threading.Tasks.Task.Delay(delayMs);
				System.Windows.Forms.SendKeys.SendWait(hotkey);
			});
		}

		private void TextToSpeech()
		{
			string text = Clipboard.GetText();
			_textToSpeechService.SpeakText(text);
		}

		private async Task SpeechToText()
		{
			if (this._activeSpeetchToTextServiceComboItem is null)
			{
				MessageBox.Show("No active speech-to-text service selected.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}



			try
			{
				if (this._speechToTextState.TranscribingAudio)
				{
					this._speechToTextState.StopTranscription();
					return;
				}

				string sessionsDirectory = Path.Combine(_settings.SpeetchToTextSettings.TempDirectory, Constants.SessionsDirectoryName);
				if (!Directory.Exists(sessionsDirectory))
					Directory.CreateDirectory(sessionsDirectory);

				string audioFilePath = Path.Combine(sessionsDirectory, "mutation_recording.mp3");

				await this._speechToTextState.AudioRecorderLock.WaitAsync().ConfigureAwait(true);
				{
					if (!this._speechToTextState.RecordingAudio)
					{
						txtSpeechToText.ReadOnly = true;
						txtSpeechToText.Text = "Recording microphone...";

						_audioRecorder = new AudioRecorder();
						_audioRecorder.StartRecording(_microphoneDeviceIndex, audioFilePath);
						btnSpeechToTextRecord.Text = "Stop &Recording";

						BeepStart();
					}
					else // Busy recording, so we want to stop it.
					{
						_audioRecorder.StopRecording();
						_audioRecorder.Dispose();
						_audioRecorder = null;

						BeepEnd ( );

						txtSpeechToText.ReadOnly = true;
						txtSpeechToText.Text = "Converting speech to text...";

						btnSpeechToTextRecord.Text = "Processing";
						btnSpeechToTextRecord.Enabled = false;

						string text = "";
						_speechToTextState.StartTranscription();
						try
						{
							if (this._activeSpeetchToTextServiceComboItem is not null)
								text = await this._activeSpeetchToTextServiceComboItem.SpeechToTextService.ConvertAudioToText(txtSpeechToTextPrompt.Text, audioFilePath, this._speechToTextState.TranscriptionCancellationTokenSource.Token).ConfigureAwait(true);
							else
								MessageBox.Show("No active speech-to-text service selected.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						}
						finally
						{
							_speechToTextState.StopTranscription();

							txtSpeechToText.ReadOnly = false;
							txtSpeechToText.Text = $"{text}";

							btnSpeechToTextRecord.Text = "&Record";
							btnSpeechToTextRecord.Enabled = true;
						}
					}
				}

			}
			catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				// This was an intentional cancellation by the user, so only beep the failure, but don't show an error message. 
				BeepFail();

				txtSpeechToText.Text = "Transcription cancelled by user.";
				txtSpeechToText.ReadOnly = false;

				btnSpeechToTextRecord.Text = "&Record";
				btnSpeechToTextRecord.Enabled = true;
			}
			catch (Exception ex)
			{
				BeepFail();

				string msg = $"Failed speech to text: {ex.Message}{Environment.NewLine}{ex.GetType().FullName}{Environment.NewLine}{ex.StackTrace}"; ;
				txtSpeechToText.Text = msg;
				txtSpeechToText.ReadOnly = false;

				btnSpeechToTextRecord.Text = "&Record";
				btnSpeechToTextRecord.Enabled = true;

				this.Activate();
				MessageBox.Show(this, msg, "Speech to text error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			finally
			{
				this._speechToTextState.AudioRecorderLock.Release();
			}
		}

		private static Hotkey MapHotKey(string hotKeyStringRepresentation)
		{
			var hotKey = new Hotkey();

			var keyStrings = hotKeyStringRepresentation.Split(@"_-+,;: ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
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
			keyString = keyString
				.Replace("{", "")
				.Replace("}", "");

			return keyString.ToLowerInvariant() switch
			{
				"del" => "delete",
				"ins" => "insert",
				_ => keyString
			};

		}

		private bool TryRegisterHotkey(Hotkey hotKey)
		{
			if (!hotKey.GetCanRegister(this))
			{
				this.Activate();
				MessageBox.Show($"Oops, looks like attempts to register the hotkey {hotKey} will fail or throw an exception.");
				return false;
			}
			else
			{
				hotKey.Register(this);
				return true;
			}
		}

		private void MutationForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			_settings.MainWindowUiSettings.WindowSize = this.Size;
			_settings.MainWindowUiSettings.WindowLocation = this.Location;

			_activeSpeetchToTextServiceComboItem.SpeetchToTextServiceSettings.SpeechToTextPrompt = txtSpeechToTextPrompt.Text;
			_settings.SpeetchToTextSettings.ActiveSpeetchToTextService = _activeSpeetchToTextServiceComboItem.SpeetchToTextServiceSettings.Name;

			_settings.LlmSettings.FormatTranscriptPrompt = txtFormatTranscriptPrompt.Text;
			_settings.LlmSettings.ReviewTranscriptPrompt = txtReviewTranscriptPrompt.Text;
			this._settingsManager.SaveSettingsToFile(_settings);

			UnregisterHotkey(_hkToggleMicMute);
			UnregisterHotkey(_hkOcr);
			BeepPlayer.DisposePlayers ( );
		}

		private static void UnregisterHotkey(Hotkey hk)
		{
			if (hk != null && hk.Registered)
				hk.Unregister();
		}

		private void MutationForm_Load(object sender, EventArgs e)
		{
			RestoreWindowLocationAndSizeFromSettings();
			//BookMark??888
			//cmbSpeechToTextService.Text =
			//	$"{ _activeSpeetchToTextServiceComboItem.Provider}: {_activeSpeetchToTextServiceComboItem.ModelId}";
		}

		private async void btnSpeechToTextRecord_Click(object sender, EventArgs e)
		{
			await SpeechToText();
		}

		private void btnClearFormattedTranscript_Click(object sender, EventArgs e)
		{
			txtFormatTranscriptResponse.Text = string.Empty;
		}

		private async Task FormatSpeechToTextTranscriptWithRules()
		{
			string rawTranscript = txtSpeechToText.Text;

			string text = rawTranscript;
			if (radManualPunctuation.Checked)
			{
				text = text.RemoveSubstrings(",", ".", ";", ":", "?", "!", "...", "…");
				text = text.Replace("  ", " ");
			}
			text = text.FormatWithRules(_settings.LlmSettings.TranscriptFormatRules);
			text = text.CleanupPunctuation();

			if (chkFormattedTranscriptAppend.Checked)
				txtFormatTranscriptResponse.Text += text;
			else
				txtFormatTranscriptResponse.Text = text;

			await Task.Delay(100);
			SetTextToClipboard(text);

			if (!this.ContainsFocus)
			{
				var selectedInsertOptionValue = cmbInsertInto3rdPartyApplication.SelectedItem;

				if (selectedInsertOptionValue is not null)
				{
					DictationInsertOption selectedOption = (DictationInsertOption)((dynamic)selectedInsertOptionValue).Value;

					switch (selectedOption)
					{
						case DictationInsertOption.SendKeys:
							BeepStart();
							System.Windows.Forms.SendKeys.Send(text);
							break;
						case DictationInsertOption.Paste:
							Thread.Sleep(200); // Wait for text to arrive on clipboard.
							BeepStart();
							System.Windows.Forms.SendKeys.SendWait("^v");
							break;
					}
				}
			}

			BeepSuccess();
		}

		private async Task FormatSpeechToTextTranscriptWithLlm()
		{
			txtFormatTranscriptResponse.Text = "Formatting...";
			BeepStart();

			string rawTranscript = txtSpeechToText.Text;
			string formatTranscriptPrompt = txtFormatTranscriptPrompt.Text;

			var messages = new List<ChatMessage>
			{
				ChatMessage.FromSystem($"{formatTranscriptPrompt}"),
				ChatMessage.FromUser($"Reformat the following transcript: {rawTranscript}"),
			};

			string formattedText = await _llmService.CreateChatCompletion(messages, Models.Gpt_4);
			txtFormatTranscriptResponse.Text = formattedText.FixNewLines();

			BeepSuccess();
		}

		private async Task ReviewSpeechToTextTranscriptWithLlm()
		{
			txtTranscriptReviewResponse.ReadOnly = true;
			txtTranscriptReviewResponse.Text = "Reviewing...";
			dgvReview.Enabled = false;
			SetReviewGridCaption("Reviewing...");

			dgvReview.Rows.Clear();
			BeepStart();

			string transcript = txtFormatTranscriptResponse.Text;
			string reviewTranscriptPrompt = txtReviewTranscriptPrompt.Text;

			var messages = new List<ChatMessage>
			{
				ChatMessage.FromSystem($"{reviewTranscriptPrompt}"),
				ChatMessage.FromUser($"Review the following transcript: {Environment.NewLine}{Environment.NewLine}{transcript}"),
			};

			var selectedTemperature = cmbReviewTemperature.SelectedItem;
			decimal temperature = ((dynamic)selectedTemperature).Value;
			string review = await _llmService.CreateChatCompletion(messages, Models.Gpt_4, temperature);
			txtTranscriptReviewResponse.Text = review.FixNewLines();
			txtTranscriptReviewResponse.ReadOnly = false;

			var lines = txtTranscriptReviewResponse.Text.Split(Environment.NewLine, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			foreach (var line in lines)
			{
				string issue = line.RemovePrefix("- ");
				dgvReview.Rows.Add(new object[] { false, issue });
			}
			dgvReview.Enabled = true;
			SetReviewGridCaption("Issue");

			BeepSuccess();
		}

		private void SetReviewGridCaption(string text)
		{
			dgvReview.Columns[1].HeaderCell.Value = text;
		}

		private void BeepMuted()
		{
			BeepPlayer.Play ( BeepType.Mute );
		}

		private void BeepUnmuted()
		{
			BeepPlayer.Play ( BeepType.Unmute );
		}

		private static void BeepStart()
		{
			BeepPlayer.Play ( BeepType.Start );
		}

		private static void BeepEnd ( )
		{
			BeepPlayer.Play ( BeepType.End );
		}

		private static void BeepSuccess()
		{
			BeepPlayer.Play ( BeepType.Success );
		}

		private static void BeepFail()
		{
			for (int i = 0; i < 3; i++)
				BeepPlayer.Play ( BeepType.Failure);
		}

		private async void btnReviewTranscript_Click(object sender, EventArgs e)
		{
			await ReviewSpeechToTextTranscriptWithLlm();
		}

		private void lblFormatTranscriptPrompt_Click(object sender, EventArgs e)
		{
			txtFormatTranscriptPrompt.Visible = !txtFormatTranscriptPrompt.Visible;
		}

		private void lblReviewTranscriptPrompt_Click(object sender, EventArgs e)
		{
			txtReviewTranscriptPrompt.Visible = !txtReviewTranscriptPrompt.Visible;
		}

		private async void txtSpeechToText_TextChanged(object sender, EventArgs e)
		{
			if (!txtSpeechToText.ReadOnly)
			{
				await FormatSpeechToTextTranscriptWithRules();
			}
		}

		private void lblTranscriptReview_Click(object sender, EventArgs e)
		{
			dgvReview.Visible = txtTranscriptReviewResponse.Visible;
			txtTranscriptReviewResponse.Visible = !dgvReview.Visible;
		}

		private async void btnApplySelectedReviewIssues_Click(object sender, EventArgs e)
		{
			await ApplyReviewActionsToFormattedTranscriptWithLlm();
		}

		private async Task ApplyReviewActionsToFormattedTranscriptWithLlm()
		{
			List<(DataGridViewRow row, string instruction)> selectedRows = new();
			foreach (DataGridViewRow row in dgvReview.Rows)
			{
				if (row.Cells[0].Value != null && (bool)row.Cells[0].Value == true)
					selectedRows.Add((row, row.Cells[1].Value.ToString()));
			}

			if (selectedRows.Any())
			{
				txtFormatTranscriptResponse.ReadOnly = true;
				dgvReview.Enabled = false;
				SetReviewGridCaption("Applying corrections...");

				BeepStart();

				string transcript = txtFormatTranscriptResponse.Text;
				string systemPrompt = txtReviewTranscriptPrompt.Text;
				string[] instructions = selectedRows
					.Select(x => $"- {x.instruction}")
					.ToArray();
				string combinedInstructions = string.Join(Environment.NewLine, instructions);

				var messages = new List<ChatMessage>
				{
					ChatMessage.FromSystem($"{systemPrompt}"),
					ChatMessage.FromUser($"Apply the corrections and respond only with the corrected transcript.{Environment.NewLine}{Environment.NewLine}Correction Instructions:{Environment.NewLine}{combinedInstructions }{Environment.NewLine}{Environment.NewLine}Transcript:{Environment.NewLine}{transcript}"),
				};

				string revision = await _llmService.CreateChatCompletion(messages, Models.Gpt_4);
				txtFormatTranscriptResponse.Text = revision.FixNewLines();
				txtFormatTranscriptResponse.ReadOnly = false;

				foreach (var (row, instruction) in selectedRows)
					dgvReview.Rows.Remove(row);
				dgvReview.Enabled = true;
				SetReviewGridCaption("Issue");

				BeepSuccess();
			}
		}

		private void dgvReview_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
		{

		}

		private void dgvReview_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
		{

		}

		private void cmbActiveMicrophone_SelectedIndexChanged(object sender, EventArgs e)
		{
			var selectedItem = cmbActiveMicrophone.SelectedItem as CaptureDeviceComboItem;
			if (selectedItem is not null)
			{
				_microphone = selectedItem.CaptureDevice;
				SelectCaptureDeviceForNAudioBasedRecording();
				_settings.AudioSettings.ActiveCaptureDeviceFullName = _microphone.FullName;
				FeedbackMicrophoneStateToUser();
			}
			else
				MessageBox.Show($"Selected item is not a {nameof(CaptureDeviceComboItem)}.", "Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		private void cmbSpeechToTextService_SelectedIndexChanged(object sender, EventArgs e)
		{
			var selectedItem = cmbSpeechToTextService.SelectedItem as SpeechToTextServiceComboItem;
			if (selectedItem is not null)
			{
				_activeSpeetchToTextServiceComboItem = selectedItem;
				txtSpeechToTextPrompt.Text = _activeSpeetchToTextServiceComboItem.SpeetchToTextServiceSettings.SpeechToTextPrompt;
			}
			else
			{
				MessageBox.Show($"Selected item is not a {nameof(SpeechToTextServiceComboItem)}.", "Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
	}
}

