using System;
using System.Drawing;
using System.Windows.Forms;
using Mutation;

namespace Mutation
{
        public partial class ScreenCaptureForm : Form
        {
                private readonly ClipboardManager _clipboardManager;
                private Bitmap? screenshot;
		private Bitmap? overlay; // New overlay for the crosshair
		private Rectangle selectedRegion;
		private Point firstPoint;

                public ScreenCaptureForm(Bitmap screenshot, ClipboardManager clipboardManager)
                {
                        _clipboardManager = clipboardManager ?? throw new ArgumentNullException(nameof(clipboardManager));
			InitializeComponent();
			if (Screen.PrimaryScreen == null)
			{
				throw new InvalidOperationException("No primary screen detected.");
			}
			this.Bounds = Screen.PrimaryScreen.Bounds;
			this.FormBorderStyle = FormBorderStyle.None; // Hide title bar
			this.WindowState = FormWindowState.Maximized;
			this.DoubleBuffered = true; // Enable double buffering
			this.screenshot = screenshot; // Receive the screenshot
			this.Cursor = Cursors.Cross; // Change the cursor to a crosshair

			// Initialize the overlay with the same size as the screenshot
			overlay = new Bitmap(screenshot.Width, screenshot.Height);
			Invalidate(); // Invalidate to refresh the form display

			// Make sure this form is always the topmost form when it opens. This is particularly useful if the main application is hidden when the hotkey is pressed to capture the screen. 
			this.Activate();
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			firstPoint = e.Location;
			base.OnMouseDown(e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			// Clear the overlay
			if (overlay != null)
			{
				using (Graphics gOverlay = Graphics.FromImage(overlay))
				{
					gOverlay.Clear(Color.Transparent);

					// Draw the crosshair on the overlay
					using (Pen crosshairPen = new Pen(Color.Green, 1))
					{
						gOverlay.DrawLine(crosshairPen, e.X, 0, e.X, Height);
						gOverlay.DrawLine(crosshairPen, 0, e.Y, Width, e.Y);
					}
				}
			}

			if (e.Button == MouseButtons.Left)
			{
				selectedRegion = new Rectangle(Math.Min(firstPoint.X, e.X), Math.Min(firstPoint.Y, e.Y),
					Math.Abs(e.X - firstPoint.X), Math.Abs(e.Y - firstPoint.Y));
			}

			Invalidate(); // Forces the form to repaint
			base.OnMouseMove(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				this.Close(); // Right-click to cancel and close the form
				return;
			}

			if (selectedRegion.Width > 0 && selectedRegion.Height > 0)
			{
				if (screenshot != null)
				{
					using (Bitmap selectedImage = new Bitmap(selectedRegion.Width, selectedRegion.Height))
					{
						using (Graphics g = Graphics.FromImage(selectedImage))
						{
							g.DrawImage(screenshot, 0, 0, selectedRegion, GraphicsUnit.Pixel);
						}

                                                _clipboardManager.SetImage(selectedImage); // Copies the image to the clipboard
					}
				}

				// Revert the cursor to the standard mouse pointer
				this.Cursor = Cursors.Default;
				this.Close(); // Explicitly close the form
			}
			base.OnMouseUp(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if (screenshot != null && overlay != null) // Check to ensure screenshot and overlay are not null
			{
				try
				{
					e.Graphics.DrawImage(screenshot, 0, 0);
					e.Graphics.DrawImage(overlay, 0, 0); // Draw the overlay containing the crosshair

					// Draw the selected region
					using (Pen pen = new Pen(Color.Red, 2))
					{
						e.Graphics.DrawRectangle(pen, selectedRegion);
					}
				}
				catch (ArgumentException)
				{
					// Ignore drawing if the image is disposed or invalid
				}
			}
			base.OnPaint(e);
		}

		private void ScreenCaptureForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			overlay?.Dispose();
			overlay = null;
			screenshot?.Dispose();
			screenshot = null;
		}
	}
}
