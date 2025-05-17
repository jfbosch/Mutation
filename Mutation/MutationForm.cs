using CognitiveSupport;
using StringExtensionLibrary;
using System.ComponentModel;

namespace Mutation;

public partial class MutationForm : Form
{
	private SpeechToTextServiceComboItem? _activeSpeetchToTextServiceComboItem = null;

	private readonly CognitiveSupport.Settings _settings;
	private readonly ISettingsManager _settingsManager;
	private readonly AudioDeviceManager _audioDeviceManager;
	private readonly ISpeechToTextService[] _speechToTextServices;
	private readonly SpeechToTextManager _speechToTextManager;
	private readonly OcrManager _ocrManager;
	private readonly ClipboardManager _clipboardManager;

	private readonly ILlmService _llmService;
	private readonly ITextToSpeechService _textToSpeechService;

	private readonly TranscriptFormatter _transcriptFormatter;
	private readonly TranscriptReviewer _transcriptReviewer;

	private readonly HotkeyManager _hotkeyManager;
	private readonly UiStateManager _uiStateManager;
	private readonly TooltipManager _tooltipManager;

	private readonly object _microphoneLock = new();

	private CancellationTokenSource _formatDebounceCts = new();

	public MutationForm(
		ISettingsManager settingsManager,
		CognitiveSupport.Settings settings,
		AudioDeviceManager audioDeviceManager,
		OcrManager ocrManager,
		ClipboardManager clipboardManager,
		ISpeechToTextService[] speechToTextServices,
		ITextToSpeechService textToSpeechService,
		ILlmService llmService,
		TranscriptFormatter transcriptFormatter,
		TranscriptReviewer transcriptReviewer,
		HotkeyManager hotkeyManager,
		UiStateManager uiStateManager)
	{
		this._settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
		this._settings = settings ?? throw new ArgumentNullException(nameof(settings));
		this._audioDeviceManager = audioDeviceManager ?? throw new ArgumentNullException(nameof(audioDeviceManager));
		this._ocrManager = ocrManager ?? throw new ArgumentNullException(nameof(ocrManager));
		this._clipboardManager = clipboardManager ?? throw new ArgumentNullException(nameof(clipboardManager));
		this._speechToTextServices = speechToTextServices ?? throw new ArgumentNullException(nameof(speechToTextServices));
		this._textToSpeechService = textToSpeechService ?? throw new ArgumentNullException(nameof(textToSpeechService));
		this._llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
		this._transcriptFormatter = transcriptFormatter ?? throw new ArgumentNullException(nameof(transcriptFormatter));
		this._transcriptReviewer = transcriptReviewer ?? throw new ArgumentNullException(nameof(transcriptReviewer));
		this._hotkeyManager = hotkeyManager ?? throw new ArgumentNullException(nameof(hotkeyManager));
		this._uiStateManager = uiStateManager ?? throw new ArgumentNullException(nameof(uiStateManager));
		this._speechToTextManager = new SpeechToTextManager(this._settings);


		InitializeComponent();
		InitializeAudioControls();

		PopulateSpeechToTextServiceCombo();

		_tooltipManager = new TooltipManager(toolTip, _settings);
		_tooltipManager.SetupTooltips(txtSpeechToTextPrompt,
		lblSpeechToTextPrompt,
		lblSpeechToText,
		lblFormatTranscriptResponse);

		txtFormatTranscriptPrompt.Text = this._settings.LlmSettings?.FormatTranscriptPrompt ?? string.Empty;
		txtReviewTranscriptPrompt.Text = this._settings.LlmSettings?.ReviewTranscriptPrompt ?? string.Empty;

		InitializeLlmReviewListView();

		cmbInsertInto3rdPartyApplication.DropDownStyle = ComboBoxStyle.DropDownList;
		foreach (DictationInsertOption option in Enum.GetValues(typeof(DictationInsertOption)))
		{
			string description = GetEnumDescription(option);
			cmbInsertInto3rdPartyApplication.Items.Add(new ComboBoxItem<DictationInsertOption> { Text = description, Value = option });
		}
		cmbInsertInto3rdPartyApplication.DisplayMember = nameof(ComboBoxItem<DictationInsertOption>.Text);
		cmbInsertInto3rdPartyApplication.ValueMember = nameof(ComboBoxItem<DictationInsertOption>.Value);
		cmbInsertInto3rdPartyApplication.SelectedIndex = 2;


		cmbReviewTemperature.DropDownStyle = ComboBoxStyle.DropDownList;
		for (decimal d = 0.0m; d < 1.9m; d = d + 0.1m)
		{
			cmbReviewTemperature.Items.Add(new ComboBoxItem<decimal> { Text = $"{d}", Value = d });
		}
		cmbReviewTemperature.DisplayMember = nameof(ComboBoxItem<decimal>.Text);
		cmbReviewTemperature.ValueMember = nameof(ComboBoxItem<decimal>.Value);
		cmbReviewTemperature.SelectedIndex = 4;
	}

	public static string GetEnumDescription(Enum value)
	{
		var fieldInfo = value.GetType().GetField(value.ToString());
		var attributes = (DescriptionAttribute[]?)fieldInfo?.GetCustomAttributes(typeof(DescriptionAttribute), false);

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



	private void SetupHotkeys()
	{
		_hotkeyManager.RegisterHotkeys(
								this,
								TakeScreenshotToClipboard,
								TakeScreenshotAndExtractText,
								ExtractTextViaOcrFromClipboardImage,
								ToggleMicrophoneMute,
								SpeechToText,
								TextToSpeech);

		lblToggleMic.Text = $"Toggle Microphone Mute: {_hotkeyManager.ToggleMicMuteHotkey}";
		lblScreenshotHotKey.Text = $"Screenshot: {_hotkeyManager.ScreenshotHotkey}";
		lblScreenshotOcrHotKey.Text = $"Screenshot OCR: {_hotkeyManager.ScreenshotOcrHotkey}";
		lblOcrHotKey.Text = $"OCR Clipboard: {_hotkeyManager.OcrHotkey}";
	}

	internal void InitializeAudioControls()
	{
		txtActiveMicrophoneMuteState.Text = "(Initializing...)";

		_audioDeviceManager.RefreshCaptureDevices();
		PopulateActiveMicrophoneCombo();
		SetActiveMicrophoneFromSettings();
		SetActiveMicrophoneToDefaultCaptureDeviceIfNotSet();
	}

	private void SetActiveMicrophoneToDefaultCaptureDeviceIfNotSet()
	{
		if (_audioDeviceManager.Microphone is null)
		{
			_audioDeviceManager.EnsureDefaultMicrophoneSelected();
			if (_audioDeviceManager.Microphone is not null)
			{
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
			if (item.CaptureDevice.FullName == _settings.AudioSettings?.ActiveCaptureDeviceFullName)
			{
				cmbActiveMicrophone.SelectedItem = item;
				_audioDeviceManager.SelectMicrophone(item.CaptureDevice);
				break;
			}
		}
	}

	private void PopulateActiveMicrophoneCombo()
	{
		cmbActiveMicrophone.Items.Clear();
		_audioDeviceManager.CaptureDevices
				  .ToList()
				  .ForEach(m => cmbActiveMicrophone.Items.Add(new CaptureDeviceComboItem
				  {
					  CaptureDevice = m,
				  }));
	}

	private void PopulateSpeechToTextServiceCombo()
	{
		cmbSpeechToTextService.Items.Clear();
		_settings.SpeetchToTextSettings?.Services
			?.ToList()
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
			if (item.SpeetchToTextServiceSettings.Name == _settings.SpeetchToTextSettings?.ActiveSpeetchToTextService)
			{
				cmbSpeechToTextService.SelectedItem = item;
				break;
			}
		}
	}

	private void SelectActiveCaptureDeviceInActiveMicrophoneCombo()
	{
		if (_audioDeviceManager.Microphone is null)
			return;

		foreach (CaptureDeviceComboItem item in cmbActiveMicrophone.Items)
		{
			if (item.CaptureDevice.FullName == _audioDeviceManager.Microphone.FullName)
			{
				cmbActiveMicrophone.SelectedItem = item;
				break;
			}
		}
	}


	public void ToggleMicrophoneMute()
	{
		lock (_microphoneLock)
		{
			_audioDeviceManager.ToggleMute();
			FeedbackMicrophoneStateToUser();
		}
	}

	private void FeedbackMicrophoneStateToUser()
	{
		lock (_microphoneLock)
		{
			if (_audioDeviceManager.Microphone?.IsMuted == true)
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

			txtActiveMicrophoneMuteState.Text = _audioDeviceManager.IsMuted ? "Muted" : "Unmuted";

			int i = 1;
			txtAllMics.Text = string.Join(Environment.NewLine, _audioDeviceManager.CaptureDevices.Select(m => $"{i++}) {m.FullName}{(m.IsMuted ? "		 - muted" : "")}").ToArray());
		}
	}


	private void TakeScreenshotToClipboard()
	{
		_ocrManager.TakeScreenshotToClipboard();
	}

	private async void TakeScreenshotAndExtractText(OcrReadingOrder ocrReadingOrder)
	{
		var result = await _ocrManager.TakeScreenshotAndExtractText(ocrReadingOrder).ConfigureAwait(true);
		txtOcr.Text = result.Message;
		HotkeyManager.SendKeysAfterDelay(_settings.AzureComputerVisionSettings?.SendKotKeyAfterOcrOperation ?? string.Empty, result.Success ? 50 : 25);
	}

	private async Task ExtractTextViaOcrFromClipboardImage(OcrReadingOrder ocrReadingOrder)
	{
		var result = await _ocrManager.ExtractTextFromClipboardImage(ocrReadingOrder).ConfigureAwait(true);
		txtOcr.Text = result.Message;
		HotkeyManager.SendKeysAfterDelay(_settings.AzureComputerVisionSettings?.SendKotKeyAfterOcrOperation ?? string.Empty, result.Success ? 50 : 25);
	}




	private void TextToSpeech()
	{
		string text = _clipboardManager.GetText();
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
			if (_speechToTextManager.Transcribing)
			{
				_speechToTextManager.CancelTranscription();
				return;
			}

			if (!_speechToTextManager.Recording)
			{
				txtSpeechToText.ReadOnly = true;
				txtSpeechToText.Text = "Recording microphone...";
				btnSpeechToTextRecord.Text = "Stop &Recording";

				await _speechToTextManager.StartRecordingAsync(_audioDeviceManager.MicrophoneDeviceIndex).ConfigureAwait(true);
				BeepStart();
			}
			else
			{
				BeepEnd();

				txtSpeechToText.ReadOnly = true;
				txtSpeechToText.Text = "Converting speech to text...";
				btnSpeechToTextRecord.Text = "Processing";
				btnSpeechToTextRecord.Enabled = false;

				string text = await _speechToTextManager.StopRecordingAndTranscribeAsync(
						  this._activeSpeetchToTextServiceComboItem.SpeechToTextService,
						  txtSpeechToTextPrompt.Text,
						  CancellationToken.None).ConfigureAwait(true);

				txtSpeechToText.ReadOnly = false;
				txtSpeechToText.Text = $"{text}";
				btnSpeechToTextRecord.Text = "&Record";
				btnSpeechToTextRecord.Enabled = true;
			}

		}
		catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
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
	}


	private void MutationForm_FormClosing(object sender, FormClosingEventArgs e)
	{
		_uiStateManager.Save(this);

		_activeSpeetchToTextServiceComboItem!.SpeetchToTextServiceSettings.SpeechToTextPrompt = txtSpeechToTextPrompt.Text;
		_settings.SpeetchToTextSettings!.ActiveSpeetchToTextService = _activeSpeetchToTextServiceComboItem!.SpeetchToTextServiceSettings.Name;

		_settings.LlmSettings!.FormatTranscriptPrompt = txtFormatTranscriptPrompt.Text;
		_settings.LlmSettings!.ReviewTranscriptPrompt = txtReviewTranscriptPrompt.Text;
		this._settingsManager.SaveSettingsToFile(_settings);

		_hotkeyManager.UnregisterHotkeys();
		BeepPlayer.DisposePlayers();
	}


	private void MutationForm_Load(object sender, EventArgs e)
	{
		_uiStateManager.Restore(this);
		SetupHotkeys();
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

		string text = _transcriptFormatter.ApplyRules(rawTranscript, radManualPunctuation.Checked);

		if (chkFormattedTranscriptAppend.Checked)
			txtFormatTranscriptResponse.Text += text;
		else
			txtFormatTranscriptResponse.Text = text;

		await Task.Delay(100);
		_clipboardManager.SetText(text);

		if (!this.ContainsFocus)
		{
			if (cmbInsertInto3rdPartyApplication.SelectedItem is ComboBoxItem<DictationInsertOption> insertOptionItem)
			{
				DictationInsertOption selectedOption = insertOptionItem.Value;

				switch (selectedOption)
				{
					case DictationInsertOption.SendKeys:
						BeepStart();
						System.Windows.Forms.SendKeys.Send(text);
						break;
					case DictationInsertOption.Paste:
						await Task.Delay(200); // Wait for text to arrive on clipboard.
						BeepStart();
						System.Windows.Forms.SendKeys.SendWait("^v");
						break;
				}
			}
		}

		BeepSuccess();
		HotkeyManager.SendKeysAfterDelay(_settings.SpeetchToTextSettings?.SendKotKeyAfterTranscriptionOperation ?? string.Empty, 50);
	}

	private async Task FormatSpeechToTextTranscriptWithLlm()
	{
		txtFormatTranscriptResponse.Text = "Formatting...";
		BeepStart();

		string rawTranscript = txtSpeechToText.Text;
		string formatTranscriptPrompt = txtFormatTranscriptPrompt.Text;

		string formattedText = await _transcriptFormatter.FormatWithLlmAsync(rawTranscript, formatTranscriptPrompt);
		txtFormatTranscriptResponse.Text = formattedText;

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

		decimal temperature = 0.4m;
		if (cmbReviewTemperature.SelectedItem is ComboBoxItem<decimal> temperatureItem)
			temperature = temperatureItem.Value;
		string review = await _transcriptReviewer.ReviewAsync(transcript, reviewTranscriptPrompt, temperature);
		txtTranscriptReviewResponse.Text = review;
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
		BeepPlayer.Play(BeepType.Mute);
	}

	private void BeepUnmuted()
	{
		BeepPlayer.Play(BeepType.Unmute);
	}

	private static void BeepStart()
	{
		BeepPlayer.Play(BeepType.Start);
	}

	private static void BeepEnd()
	{
		BeepPlayer.Play(BeepType.End);
	}

	private static void BeepSuccess()
	{
		BeepPlayer.Play(BeepType.Success);
	}

	private static void BeepFail()
	{
		for (int i = 0; i < 3; i++)
			BeepPlayer.Play(BeepType.Failure);
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
		if (txtSpeechToText.ReadOnly)
			return;

		_formatDebounceCts.Cancel();
		_formatDebounceCts = new CancellationTokenSource();
		var token = _formatDebounceCts.Token;

		try
		{
			await Task.Delay(300, token);
			if (!token.IsCancellationRequested)
				await FormatSpeechToTextTranscriptWithRules();
		}
		catch (TaskCanceledException)
		{
			// ignore
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
		List<(DataGridViewRow row, string? instruction)> selectedRows = new();
		foreach (DataGridViewRow row in dgvReview.Rows)
		{
			if (row.Cells[0].Value != null && (bool)row.Cells[0].Value == true)
			{
				var instruction = row.Cells[1].Value?.ToString() ?? string.Empty;
				selectedRows.Add((row, instruction));
			}
		}

		if (selectedRows.Any())
		{
			txtFormatTranscriptResponse.ReadOnly = true;
			dgvReview.Enabled = false;
			SetReviewGridCaption("Applying corrections...");

			BeepStart();

			string transcript = txtFormatTranscriptResponse.Text;
			string systemPrompt = txtReviewTranscriptPrompt.Text;
			string[] instructions = selectedRows.Select(x => x.instruction ?? string.Empty).ToArray();

			string revision = await _transcriptReviewer.ApplyCorrectionsAsync(transcript, systemPrompt, instructions);
			txtFormatTranscriptResponse.Text = revision;
			txtFormatTranscriptResponse.ReadOnly = false;

			foreach (var (row, instruction) in selectedRows)
				dgvReview.Rows.Remove(row);
			dgvReview.Enabled = true;
			SetReviewGridCaption("Issue");

			BeepSuccess();
		}
	}


	private void cmbActiveMicrophone_SelectedIndexChanged(object sender, EventArgs e)
	{
		var selectedItem = cmbActiveMicrophone.SelectedItem as CaptureDeviceComboItem;
		if (selectedItem is not null)
		{
			_audioDeviceManager.SelectMicrophone(selectedItem.CaptureDevice);
			_settings.AudioSettings!.ActiveCaptureDeviceFullName = selectedItem.CaptureDevice.FullName;
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

