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
			lblFormatTranscriptPrompt = new Label();
			txtFormatTranscriptPrompt = new TextBox();
			btnFormatTranscript = new Button();
			txtFormatTranscriptResponse = new TextBox();
			lblFormatTranscriptResponse = new Label();
			chkAutoFormatTranscript = new CheckBox();
			splitContainerLlmProcessing = new SplitContainer();
			lblReviewTranscriptPrompt = new Label();
			chkAutoReviewTranscript = new CheckBox();
			txtReviewTranscriptPrompt = new TextBox();
			btnReviewTranscript = new Button();
			lblTranscriptReview = new Label();
			txtTranscriptReviewResponse = new TextBox();
			((System.ComponentModel.ISupportInitialize)splitContainerLlmProcessing).BeginInit();
			splitContainerLlmProcessing.Panel1.SuspendLayout();
			splitContainerLlmProcessing.Panel2.SuspendLayout();
			splitContainerLlmProcessing.SuspendLayout();
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
			txtSpeechToText.Size = new Size(927, 93);
			txtSpeechToText.TabIndex = 12;
			txtSpeechToText.TextChanged += txtSpeechToText_TextChanged;
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
			// lblFormatTranscriptPrompt
			// 
			lblFormatTranscriptPrompt.AutoSize = true;
			lblFormatTranscriptPrompt.Location = new Point(7, 2);
			lblFormatTranscriptPrompt.Margin = new Padding(4, 0, 4, 0);
			lblFormatTranscriptPrompt.Name = "lblFormatTranscriptPrompt";
			lblFormatTranscriptPrompt.Size = new Size(142, 15);
			lblFormatTranscriptPrompt.TabIndex = 13;
			lblFormatTranscriptPrompt.Text = "Format Transcript prompt";
			lblFormatTranscriptPrompt.Click += lblFormatTranscriptPrompt_Click;
			// 
			// txtFormatTranscriptPrompt
			// 
			txtFormatTranscriptPrompt.AcceptsReturn = true;
			txtFormatTranscriptPrompt.AccessibleDescription = "The speech-to-text prompt for potentially improving accuracy.";
			txtFormatTranscriptPrompt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtFormatTranscriptPrompt.Location = new Point(7, 20);
			txtFormatTranscriptPrompt.Margin = new Padding(4, 3, 4, 3);
			txtFormatTranscriptPrompt.Multiline = true;
			txtFormatTranscriptPrompt.Name = "txtFormatTranscriptPrompt";
			txtFormatTranscriptPrompt.ScrollBars = ScrollBars.Vertical;
			txtFormatTranscriptPrompt.Size = new Size(564, 64);
			txtFormatTranscriptPrompt.TabIndex = 14;
			txtFormatTranscriptPrompt.Visible = false;
			// 
			// btnFormatTranscript
			// 
			btnFormatTranscript.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnFormatTranscript.Location = new Point(404, 90);
			btnFormatTranscript.Name = "btnFormatTranscript";
			btnFormatTranscript.Size = new Size(149, 23);
			btnFormatTranscript.TabIndex = 16;
			btnFormatTranscript.Text = "&Format Transcription";
			btnFormatTranscript.UseVisualStyleBackColor = true;
			btnFormatTranscript.Click += btnFormatTranscript_Click;
			// 
			// txtFormatTranscriptResponse
			// 
			txtFormatTranscriptResponse.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			txtFormatTranscriptResponse.Location = new Point(7, 118);
			txtFormatTranscriptResponse.Margin = new Padding(4, 3, 4, 3);
			txtFormatTranscriptResponse.Multiline = true;
			txtFormatTranscriptResponse.Name = "txtFormatTranscriptResponse";
			txtFormatTranscriptResponse.ScrollBars = ScrollBars.Vertical;
			txtFormatTranscriptResponse.Size = new Size(564, 217);
			txtFormatTranscriptResponse.TabIndex = 17;
			// 
			// lblFormatTranscriptResponse
			// 
			lblFormatTranscriptResponse.AutoSize = true;
			lblFormatTranscriptResponse.Location = new Point(7, 94);
			lblFormatTranscriptResponse.Margin = new Padding(4, 0, 4, 0);
			lblFormatTranscriptResponse.Name = "lblFormatTranscriptResponse";
			lblFormatTranscriptResponse.Size = new Size(116, 15);
			lblFormatTranscriptResponse.TabIndex = 15;
			lblFormatTranscriptResponse.Text = "Formatted Transcript";
			// 
			// chkAutoFormatTranscript
			// 
			chkAutoFormatTranscript.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			chkAutoFormatTranscript.Checked = true;
			chkAutoFormatTranscript.CheckState = CheckState.Checked;
			chkAutoFormatTranscript.Location = new Point(254, 90);
			chkAutoFormatTranscript.Name = "chkAutoFormatTranscript";
			chkAutoFormatTranscript.Size = new Size(149, 25);
			chkAutoFormatTranscript.TabIndex = 16;
			chkAutoFormatTranscript.Text = "&Auto format transcript";
			chkAutoFormatTranscript.UseVisualStyleBackColor = true;
			// 
			// splitContainerLlmProcessing
			// 
			splitContainerLlmProcessing.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			splitContainerLlmProcessing.Location = new Point(4, 369);
			splitContainerLlmProcessing.Name = "splitContainerLlmProcessing";
			// 
			// splitContainerLlmProcessing.Panel1
			// 
			splitContainerLlmProcessing.Panel1.Controls.Add(lblFormatTranscriptPrompt);
			splitContainerLlmProcessing.Panel1.Controls.Add(chkAutoFormatTranscript);
			splitContainerLlmProcessing.Panel1.Controls.Add(txtFormatTranscriptPrompt);
			splitContainerLlmProcessing.Panel1.Controls.Add(btnFormatTranscript);
			splitContainerLlmProcessing.Panel1.Controls.Add(lblFormatTranscriptResponse);
			splitContainerLlmProcessing.Panel1.Controls.Add(txtFormatTranscriptResponse);
			// 
			// splitContainerLlmProcessing.Panel2
			// 
			splitContainerLlmProcessing.Panel2.Controls.Add(lblReviewTranscriptPrompt);
			splitContainerLlmProcessing.Panel2.Controls.Add(chkAutoReviewTranscript);
			splitContainerLlmProcessing.Panel2.Controls.Add(txtReviewTranscriptPrompt);
			splitContainerLlmProcessing.Panel2.Controls.Add(btnReviewTranscript);
			splitContainerLlmProcessing.Panel2.Controls.Add(lblTranscriptReview);
			splitContainerLlmProcessing.Panel2.Controls.Add(txtTranscriptReviewResponse);
			splitContainerLlmProcessing.Size = new Size(1175, 342);
			splitContainerLlmProcessing.SplitterDistance = 582;
			splitContainerLlmProcessing.TabIndex = 18;
			// 
			// lblReviewTranscriptPrompt
			// 
			lblReviewTranscriptPrompt.AutoSize = true;
			lblReviewTranscriptPrompt.Location = new Point(4, 3);
			lblReviewTranscriptPrompt.Margin = new Padding(4, 0, 4, 0);
			lblReviewTranscriptPrompt.Name = "lblReviewTranscriptPrompt";
			lblReviewTranscriptPrompt.Size = new Size(141, 15);
			lblReviewTranscriptPrompt.TabIndex = 18;
			lblReviewTranscriptPrompt.Text = "Review Transcript prompt";
			lblReviewTranscriptPrompt.Click += lblReviewTranscriptPrompt_Click;
			// 
			// chkAutoReviewTranscript
			// 
			chkAutoReviewTranscript.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			chkAutoReviewTranscript.Location = new Point(254, 91);
			chkAutoReviewTranscript.Name = "chkAutoReviewTranscript";
			chkAutoReviewTranscript.Size = new Size(153, 25);
			chkAutoReviewTranscript.TabIndex = 21;
			chkAutoReviewTranscript.Text = "A&uto review transcript";
			chkAutoReviewTranscript.UseVisualStyleBackColor = true;
			chkAutoReviewTranscript.Visible = false;
			// 
			// txtReviewTranscriptPrompt
			// 
			txtReviewTranscriptPrompt.AcceptsReturn = true;
			txtReviewTranscriptPrompt.AccessibleDescription = "The speech-to-text prompt for potentially improving accuracy.";
			txtReviewTranscriptPrompt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtReviewTranscriptPrompt.Location = new Point(4, 21);
			txtReviewTranscriptPrompt.Margin = new Padding(4, 3, 4, 3);
			txtReviewTranscriptPrompt.Multiline = true;
			txtReviewTranscriptPrompt.Name = "txtReviewTranscriptPrompt";
			txtReviewTranscriptPrompt.ScrollBars = ScrollBars.Vertical;
			txtReviewTranscriptPrompt.Size = new Size(578, 64);
			txtReviewTranscriptPrompt.TabIndex = 19;
			// 
			// btnReviewTranscript
			// 
			btnReviewTranscript.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnReviewTranscript.Location = new Point(414, 91);
			btnReviewTranscript.Name = "btnReviewTranscript";
			btnReviewTranscript.Size = new Size(149, 23);
			btnReviewTranscript.TabIndex = 22;
			btnReviewTranscript.Text = "Re&view Transcript";
			btnReviewTranscript.UseVisualStyleBackColor = true;
			btnReviewTranscript.Click += btnReviewTranscript_Click;
			// 
			// lblTranscriptReview
			// 
			lblTranscriptReview.AutoSize = true;
			lblTranscriptReview.Location = new Point(4, 95);
			lblTranscriptReview.Margin = new Padding(4, 0, 4, 0);
			lblTranscriptReview.Name = "lblTranscriptReview";
			lblTranscriptReview.Size = new Size(44, 15);
			lblTranscriptReview.TabIndex = 20;
			lblTranscriptReview.Text = "Review";
			// 
			// txtTranscriptReviewResponse
			// 
			txtTranscriptReviewResponse.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			txtTranscriptReviewResponse.Location = new Point(4, 119);
			txtTranscriptReviewResponse.Margin = new Padding(4, 3, 4, 3);
			txtTranscriptReviewResponse.Multiline = true;
			txtTranscriptReviewResponse.Name = "txtTranscriptReviewResponse";
			txtTranscriptReviewResponse.ScrollBars = ScrollBars.Vertical;
			txtTranscriptReviewResponse.Size = new Size(578, 217);
			txtTranscriptReviewResponse.TabIndex = 23;
			// 
			// MutationForm
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(1184, 711);
			Controls.Add(splitContainerLlmProcessing);
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
			splitContainerLlmProcessing.Panel1.ResumeLayout(false);
			splitContainerLlmProcessing.Panel1.PerformLayout();
			splitContainerLlmProcessing.Panel2.ResumeLayout(false);
			splitContainerLlmProcessing.Panel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)splitContainerLlmProcessing).EndInit();
			splitContainerLlmProcessing.ResumeLayout(false);
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
		private Label lblFormatTranscriptPrompt;
		private TextBox txtFormatTranscriptPrompt;
		private Button btnFormatTranscript;
		private TextBox txtFormatTranscriptResponse;
		private Label lblFormatTranscriptResponse;
		private CheckBox chkAutoFormatTranscript;
		private SplitContainer splitContainerLlmProcessing;
		private Label lblReviewTranscriptPrompt;
		private CheckBox chkAutoReviewTranscript;
		private TextBox txtReviewTranscriptPrompt;
		private Button btnReviewTranscript;
		private Label lblTranscriptReview;
		private TextBox txtTranscriptReviewResponse;
	}
}

