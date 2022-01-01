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
			this.lblActiveMic = new System.Windows.Forms.Label();
			this.lbl2 = new System.Windows.Forms.Label();
			this.txtActiveMic = new System.Windows.Forms.TextBox();
			this.txtAllMics = new System.Windows.Forms.TextBox();
			this.SuspendLayout();
			// 
			// lblActiveMic
			// 
			this.lblActiveMic.AutoSize = true;
			this.lblActiveMic.Location = new System.Drawing.Point(137, 13);
			this.lblActiveMic.Name = "lblActiveMic";
			this.lblActiveMic.Size = new System.Drawing.Size(57, 13);
			this.lblActiveMic.TabIndex = 0;
			this.lblActiveMic.Text = "Active Mic";
			// 
			// lbl2
			// 
			this.lbl2.AutoSize = true;
			this.lbl2.Location = new System.Drawing.Point(15, 41);
			this.lbl2.Name = "lbl2";
			this.lbl2.Size = new System.Drawing.Size(16, 13);
			this.lbl2.TabIndex = 1;
			this.lbl2.Text = "...";
			// 
			// txtActiveMic
			// 
			this.txtActiveMic.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.txtActiveMic.Location = new System.Drawing.Point(216, 10);
			this.txtActiveMic.Name = "txtActiveMic";
			this.txtActiveMic.ReadOnly = true;
			this.txtActiveMic.Size = new System.Drawing.Size(257, 20);
			this.txtActiveMic.TabIndex = 2;
			// 
			// txtAllMics
			// 
			this.txtAllMics.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.txtAllMics.Location = new System.Drawing.Point(216, 36);
			this.txtAllMics.Multiline = true;
			this.txtAllMics.Name = "txtAllMics";
			this.txtAllMics.ReadOnly = true;
			this.txtAllMics.Size = new System.Drawing.Size(257, 58);
			this.txtAllMics.TabIndex = 3;
			// 
			// MutationForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(477, 100);
			this.Controls.Add(this.txtAllMics);
			this.Controls.Add(this.txtActiveMic);
			this.Controls.Add(this.lbl2);
			this.Controls.Add(this.lblActiveMic);
			this.Name = "MutationForm";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "Mutation";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MutationForm_FormClosing);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label lblActiveMic;
		private System.Windows.Forms.Label lbl2;
		private System.Windows.Forms.TextBox txtActiveMic;
		private System.Windows.Forms.TextBox txtAllMics;
	}
}

