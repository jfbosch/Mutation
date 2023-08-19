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
			lblToggleMic.TabIndex = 1;
			lblToggleMic.Text = "...";
			// 
			// txtActiveMic
			// 
			txtActiveMic.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			txtActiveMic.Location = new Point(851, 12);
			txtActiveMic.Margin = new Padding(4, 3, 4, 3);
			txtActiveMic.Name = "txtActiveMic";
			txtActiveMic.ReadOnly = true;
			txtActiveMic.Size = new Size(299, 23);
			txtActiveMic.TabIndex = 2;
			// 
			// txtAllMics
			// 
			txtAllMics.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtAllMics.Location = new Point(252, 42);
			txtAllMics.Margin = new Padding(4, 3, 4, 3);
			txtAllMics.Multiline = true;
			txtAllMics.Name = "txtAllMics";
			txtAllMics.ReadOnly = true;
			txtAllMics.Size = new Size(898, 66);
			txtAllMics.TabIndex = 3;
			// 
			// lblOcrHotKey
			// 
			lblOcrHotKey.AutoSize = true;
			lblOcrHotKey.Location = new Point(18, 114);
			lblOcrHotKey.Margin = new Padding(4, 0, 4, 0);
			lblOcrHotKey.Name = "lblOcrHotKey";
			lblOcrHotKey.Size = new Size(16, 15);
			lblOcrHotKey.TabIndex = 4;
			lblOcrHotKey.Text = "...";
			// 
			// lblSpeechToText
			// 
			lblSpeechToText.AutoSize = true;
			lblSpeechToText.Location = new Point(18, 434);
			lblSpeechToText.Margin = new Padding(4, 0, 4, 0);
			lblSpeechToText.Name = "lblSpeechToText";
			lblSpeechToText.Size = new Size(16, 15);
			lblSpeechToText.TabIndex = 5;
			lblSpeechToText.Text = "...";
			// 
			// txtSpeechToText
			// 
			txtSpeechToText.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtSpeechToText.Location = new Point(252, 394);
			txtSpeechToText.Margin = new Padding(4, 3, 4, 3);
			txtSpeechToText.Multiline = true;
			txtSpeechToText.Name = "txtSpeechToText";
			txtSpeechToText.ReadOnly = true;
			txtSpeechToText.Size = new Size(898, 116);
			txtSpeechToText.TabIndex = 6;
			// 
			// txtOcr
			// 
			txtOcr.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtOcr.Location = new Point(252, 114);
			txtOcr.Margin = new Padding(4, 3, 4, 3);
			txtOcr.Multiline = true;
			txtOcr.Name = "txtOcr";
			txtOcr.ReadOnly = true;
			txtOcr.Size = new Size(898, 116);
			txtOcr.TabIndex = 7;
			// 
			// lblScreenshotOcrHotKey
			// 
			lblScreenshotOcrHotKey.AutoSize = true;
			lblScreenshotOcrHotKey.Location = new Point(18, 414);
			lblScreenshotOcrHotKey.Margin = new Padding(4, 0, 4, 0);
			lblScreenshotOcrHotKey.Name = "lblScreenshotOcrHotKey";
			lblScreenshotOcrHotKey.Size = new Size(16, 15);
			lblScreenshotOcrHotKey.TabIndex = 8;
			lblScreenshotOcrHotKey.Text = "...";
			// 
			// lblScreenshotHotKey
			// 
			lblScreenshotHotKey.AutoSize = true;
			lblScreenshotHotKey.Location = new Point(18, 394);
			lblScreenshotHotKey.Margin = new Padding(4, 0, 4, 0);
			lblScreenshotHotKey.Name = "lblScreenshotHotKey";
			lblScreenshotHotKey.Size = new Size(16, 15);
			lblScreenshotHotKey.TabIndex = 9;
			lblScreenshotHotKey.Text = "...";
			// 
			// txtSpeechToTextPrompt
			// 
			txtSpeechToTextPrompt.AcceptsReturn = true;
			txtSpeechToTextPrompt.AccessibleDescription = "The speech-to-text prompt for potentially improving accuracy.";
			txtSpeechToTextPrompt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			txtSpeechToTextPrompt.Location = new Point(252, 322);
			txtSpeechToTextPrompt.Margin = new Padding(4, 3, 4, 3);
			txtSpeechToTextPrompt.Multiline = true;
			txtSpeechToTextPrompt.Name = "txtSpeechToTextPrompt";
			txtSpeechToTextPrompt.Size = new Size(898, 66);
			txtSpeechToTextPrompt.TabIndex = 10;
			txtSpeechToTextPrompt.MouseLeave += txtSpeechToTextPrompt_MouseLeave;
			txtSpeechToTextPrompt.MouseHover += txtSpeechToTextPrompt_MouseHover;
			// 
			// lblSpeechToTextPrompt
			// 
			lblSpeechToTextPrompt.AutoSize = true;
			lblSpeechToTextPrompt.Location = new Point(18, 322);
			lblSpeechToTextPrompt.Margin = new Padding(4, 0, 4, 0);
			lblSpeechToTextPrompt.Name = "lblSpeechToTextPrompt";
			lblSpeechToTextPrompt.Size = new Size(127, 15);
			lblSpeechToTextPrompt.TabIndex = 11;
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
			// MutationForm
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(1155, 530);
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
	}
}

