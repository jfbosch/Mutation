namespace Mutation
{
	partial class MutationForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			components = new System.ComponentModel.Container();
			lblActiveMic = new Label();
			lblToggleMic = new Label();
			txtActiveMic = new TextBox();
			txtAllMics = new TextBox();
			lblOcrHotKey = new Label();
			lblSpeechToText = new Label();
			txtSpeechToText = new TextBox();
			txtOcr = new TextBox();
			lblScreenshotOcrHotKey = new Label();
			lblScreenshotHotKey = new Label();
			txtSpeechToTextPrompt = new TextBox();
			lblSpeechToTextPrompt = new Label();
			toolTip = new ToolTip(components);
			btnSpeechToTextRecord = new Button();
			lblProofreadingPrompt = new Label();
			txtFormatTranscriptPrompt = new TextBox();
			btnFormatTranscript = new Button();
			txtFormatTranscriptResponse = new TextBox();
			lblProofreadingResponse = new Label();
			chkAutoFormatTranscript = new CheckBox();
			SuspendLayout();
			// 
			// lblActiveMic
			// 
			lblActiveMic.AutoSize = true;
			lblActiveMic.Location = new Point(160, 15);
			lblActiveMic.Margin = new Padding(4, 0, 4, 0);
			lblActiveMic.Name = "lblActiveMic";
			lblActiveMic.Size = new Size(63, 15);
			lblActiveMic.TabIndex = 0;
			lblActiveMic.Text = "Active Mic";
			// 
			// lblToggleMic
			// 
			lblToggleMic.AutoSize = true;
			lblToggleMic.Location = new Point(18, 47);
			lblToggleMic.Margin = new Padding(4, 0, 4, 0);
			lblToggleMic.Name = "lblToggleMic";
			lblToggleMic.Size = new Size(16, 15);
			lblToggleMic.TabIndex = 2;
			lblToggleMic.Text = "...";
			// 
			// txtActiveMic
			// 
			txtActiveMic.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			txtActiveMic.Location = new Point(252, 12);
			txtActiveMic.Margin = new Padding(4, 3, 4, 3);
			txtActiveMic.Name = "txtActiveMic";
			txtActiveMic.ReadOnly = true;
			txtActiveMic.Size = new Size(927, 23);
			txtActiveMic.TabIndex = 1;
			// 
			// txtAllMics
			// 
			txtAllMics.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtAllMics.Location = new Point(252, 42);
			txtAllMics.Margin = new Padding(4, 3, 4, 3);
			txtAllMics.Multiline = true;
			txtAllMics.Name = "txtAllMics";
			txtAllMics.ReadOnly = true;
			txtAllMics.ScrollBars = ScrollBars.Vertical;
			txtAllMics.Size = new Size(927, 66);
			txtAllMics.TabIndex = 3;
			// 
			// lblOcrHotKey
			// 
			lblOcrHotKey.AutoSize = true;
			lblOcrHotKey.Location = new Point(18, 134);
			lblOcrHotKey.Margin = new Padding(4, 0, 4, 0);
			lblOcrHotKey.Name = "lblOcrHotKey";
			lblOcrHotKey.Size = new Size(16, 15);
			lblOcrHotKey.TabIndex = 5;
			lblOcrHotKey.Text = "...";
			// 
			// lblSpeechToText
			// 
			lblSpeechToText.AutoSize = true;
			lblSpeechToText.Location = new Point(18, 271);
			lblSpeechToText.Margin = new Padding(4, 0, 4, 0);
			lblSpeechToText.Name = "lblSpeechToText";
			lblSpeechToText.Size = new Size(16, 15);
			lblSpeechToText.TabIndex = 10;
			lblSpeechToText.Text = "...";
			// 
			// txtSpeechToText
			// 
			txtSpeechToText.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtSpeechToText.Location = new Point(252, 271);
			txtSpeechToText.Margin = new Padding(4, 3, 4, 3);
			txtSpeechToText.Multiline = true;
			txtSpeechToText.Name = "txtSpeechToText";
			txtSpeechToText.ScrollBars = ScrollBars.Vertical;
			txtSpeechToText.Size = new Size(927, 126);
			txtSpeechToText.TabIndex = 12;
			// 
			// txtOcr
			// 
			txtOcr.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtOcr.Location = new Point(252, 114);
			txtOcr.Margin = new Padding(4, 3, 4, 3);
			txtOcr.Multiline = true;
			txtOcr.Name = "txtOcr";
			txtOcr.ReadOnly = true;
			txtOcr.ScrollBars = ScrollBars.Vertical;
			txtOcr.Size = new Size(927, 79);
			txtOcr.TabIndex = 7;
			// 
			// lblScreenshotOcrHotKey
			// 
			lblScreenshotOcrHotKey.AutoSize = true;
			lblScreenshotOcrHotKey.Location = new Point(18, 158);
			lblScreenshotOcrHotKey.Margin = new Padding(4, 0, 4, 0);
			lblScreenshotOcrHotKey.Name = "lblScreenshotOcrHotKey";
			lblScreenshotOcrHotKey.Size = new Size(16, 15);
			lblScreenshotOcrHotKey.TabIndex = 6;
			lblScreenshotOcrHotKey.Text = "...";
			// 
			// lblScreenshotHotKey
			// 
			lblScreenshotHotKey.AutoSize = true;
			lblScreenshotHotKey.Location = new Point(18, 114);
			lblScreenshotHotKey.Margin = new Padding(4, 0, 4, 0);
			lblScreenshotHotKey.Name = "lblScreenshotHotKey";
			lblScreenshotHotKey.Size = new Size(16, 15);
			lblScreenshotHotKey.TabIndex = 4;
			lblScreenshotHotKey.Text = "...";
			// 
			// txtSpeechToTextPrompt
			// 
			txtSpeechToTextPrompt.AcceptsReturn = true;
			txtSpeechToTextPrompt.AccessibleDescription = "The speech-to-text prompt for potentially improving accuracy.";
			txtSpeechToTextPrompt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtSpeechToTextPrompt.Location = new Point(252, 199);
			txtSpeechToTextPrompt.Margin = new Padding(4, 3, 4, 3);
			txtSpeechToTextPrompt.Multiline = true;
			txtSpeechToTextPrompt.Name = "txtSpeechToTextPrompt";
			txtSpeechToTextPrompt.ScrollBars = ScrollBars.Vertical;
			txtSpeechToTextPrompt.Size = new Size(927, 66);
			txtSpeechToTextPrompt.TabIndex = 9;
			// 
			// lblSpeechToTextPrompt
			// 
			lblSpeechToTextPrompt.AutoSize = true;
			lblSpeechToTextPrompt.Location = new Point(18, 199);
			lblSpeechToTextPrompt.Margin = new Padding(4, 0, 4, 0);
			lblSpeechToTextPrompt.Name = "lblSpeechToTextPrompt";
			lblSpeechToTextPrompt.Size = new Size(127, 15);
			lblSpeechToTextPrompt.TabIndex = 8;
			lblSpeechToTextPrompt.Text = "Speech To Text Prompt";
			// 
			// toolTip
			// 
			toolTip.AutomaticDelay = 300;
			toolTip.AutoPopDelay = 2000;
			toolTip.InitialDelay = 300;
			toolTip.ReshowDelay = 60;
			toolTip.ToolTipTitle = "Whisper Speech To Text Prompt";
			// 
			// btnSpeechToTextRecord
			// 
			btnSpeechToTextRecord.Location = new Point(18, 300);
			btnSpeechToTextRecord.Name = "btnSpeechToTextRecord";
			btnSpeechToTextRecord.Size = new Size(110, 23);
			btnSpeechToTextRecord.TabIndex = 11;
			btnSpeechToTextRecord.Text = "&Record";
			btnSpeechToTextRecord.UseVisualStyleBackColor = true;
			btnSpeechToTextRecord.Click += btnSpeechToTextRecord_Click;
			// 
			// lblProofreadingPrompt
			// 
			lblProofreadingPrompt.AutoSize = true;
			lblProofreadingPrompt.Location = new Point(18, 403);
			lblProofreadingPrompt.Margin = new Padding(4, 0, 4, 0);
			lblProofreadingPrompt.Name = "lblProofreadingPrompt";
			lblProofreadingPrompt.Size = new Size(119, 15);
			lblProofreadingPrompt.TabIndex = 13;
			lblProofreadingPrompt.Text = "Proofreading prompt";
			// 
			// txtFormatTranscriptPrompt
			// 
			txtFormatTranscriptPrompt.AcceptsReturn = true;
			txtFormatTranscriptPrompt.AccessibleDescription = "The speech-to-text prompt for potentially improving accuracy.";
			txtFormatTranscriptPrompt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtFormatTranscriptPrompt.Location = new Point(252, 403);
			txtFormatTranscriptPrompt.Margin = new Padding(4, 3, 4, 3);
			txtFormatTranscriptPrompt.Multiline = true;
			txtFormatTranscriptPrompt.Name = "txtFormatTranscriptPrompt";
			txtFormatTranscriptPrompt.ScrollBars = ScrollBars.Vertical;
			txtFormatTranscriptPrompt.Size = new Size(927, 66);
			txtFormatTranscriptPrompt.TabIndex = 14;
			// 
			// btnFormatTranscription
			// 
			btnFormatTranscript.Location = new Point(18, 504);
			btnFormatTranscript.Name = "btnFormatTranscription";
			btnFormatTranscript.Size = new Size(149, 23);
			btnFormatTranscript.TabIndex = 16;
			btnFormatTranscript.Text = "&Format Transcription";
			btnFormatTranscript.UseVisualStyleBackColor = true;
			btnFormatTranscript.Click += btnFormatTranscript_Click;
			// 
			// txtFormatTranscriptResponse
			// 
			txtFormatTranscriptResponse.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			txtFormatTranscriptResponse.Location = new Point(252, 475);
			txtFormatTranscriptResponse.Margin = new Padding(4, 3, 4, 3);
			txtFormatTranscriptResponse.Multiline = true;
			txtFormatTranscriptResponse.Name = "txtFormatTranscriptResponse";
			txtFormatTranscriptResponse.ScrollBars = ScrollBars.Vertical;
			txtFormatTranscriptResponse.Size = new Size(927, 234);
			txtFormatTranscriptResponse.TabIndex = 17;
			// 
			// lblProofreadingResponse
			// 
			lblProofreadingResponse.AutoSize = true;
			lblProofreadingResponse.Location = new Point(18, 475);
			lblProofreadingResponse.Margin = new Padding(4, 0, 4, 0);
			lblProofreadingResponse.Name = "lblProofreadingResponse";
			lblProofreadingResponse.Size = new Size(16, 15);
			lblProofreadingResponse.TabIndex = 15;
			lblProofreadingResponse.Text = "...";
			// 
			// chkAutoFormatTranscription
			// 
			chkAutoFormatTranscript.Location = new Point(18, 533);
			chkAutoFormatTranscript.Name = "chkAutoFormatTranscription";
			chkAutoFormatTranscript.Size = new Size(185, 25);
			chkAutoFormatTranscript.TabIndex = 16;
			chkAutoFormatTranscript.Text = "&Auto format transcription.";
			chkAutoFormatTranscript.UseVisualStyleBackColor = true;
			// 
			// MutationForm
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(1184, 711);
			Controls.Add(chkAutoFormatTranscript);
			Controls.Add(btnFormatTranscript);
			Controls.Add(txtFormatTranscriptResponse);
			Controls.Add(lblProofreadingResponse);
			Controls.Add(lblProofreadingPrompt);
			Controls.Add(txtFormatTranscriptPrompt);
			Controls.Add(btnSpeechToTextRecord);
			Controls.Add(lblSpeechToTextPrompt);
			Controls.Add(txtSpeechToTextPrompt);
			Controls.Add(lblScreenshotHotKey);
			Controls.Add(lblScreenshotOcrHotKey);
			Controls.Add(txtOcr);
			Controls.Add(txtSpeechToText);
			Controls.Add(lblSpeechToText);
			Controls.Add(lblOcrHotKey);
			Controls.Add(txtAllMics);
			Controls.Add(txtActiveMic);
			Controls.Add(lblToggleMic);
			Controls.Add(lblActiveMic);
			Margin = new Padding(4, 3, 4, 3);
			Name = "MutationForm";
			StartPosition = FormStartPosition.CenterScreen;
			Text = "Mutation";
			FormClosing += MutationForm_FormClosing;
			Load += MutationForm_Load;
			ResumeLayout(false);
			PerformLayout();
		}

		#endregion

		private Label lblActiveMic;
		private Label lblToggleMic;
		private TextBox txtActiveMic;
		private TextBox txtAllMics;
		private Label lblOcrHotKey;
		private Label lblSpeechToText;
		private TextBox txtSpeechToText;
		private TextBox txtOcr;
		private Label lblScreenshotOcrHotKey;
		private Label lblScreenshotHotKey;
		private TextBox txtSpeechToTextPrompt;
		private Label lblSpeechToTextPrompt;
		private ToolTip toolTip;
		private Button btnSpeechToTextRecord;
		private Label lblProofreadingPrompt;
		private TextBox txtFormatTranscriptPrompt;
		private Button btnFormatTranscript;
		private TextBox txtFormatTranscriptResponse;
		private Label lblProofreadingResponse;
		private CheckBox chkAutoFormatTranscript;
	}
}

