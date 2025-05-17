using CognitiveSupport;
using ScreenCapturing;
using System.Drawing.Imaging;

namespace Mutation;

/// <summary>
/// Encapsulates screenshot capturing and OCR extraction logic.
/// </summary>
public class OcrManager
{
	private readonly Settings _settings;
	private readonly IOcrService _ocrService;
	private readonly OcrState _ocrState = new();

	private ScreenCaptureForm? _activeScreenCaptureForm;

	public OcrManager(Settings settings, IOcrService ocrService)
	{
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));
		_ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
	}

	public void TakeScreenshotToClipboard()
	{
		if (_activeScreenCaptureForm is not null)
		{
			_activeScreenCaptureForm.Activate();
			return;
		}

		if (Screen.PrimaryScreen == null)
			throw new InvalidOperationException("No primary screen detected.");

		using Bitmap screenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
		using Graphics g = Graphics.FromImage(screenshot);
		g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);

		var displayShot = screenshot;
		using Bitmap invertedScreenshot = InvertScreenshotColors(screenshot);
		if (_settings.AzureComputerVisionSettings != null && _settings.AzureComputerVisionSettings.InvertScreenshot)
			displayShot = invertedScreenshot;

		using ScreenCaptureForm screenCaptureForm = new ScreenCaptureForm(new Bitmap(displayShot));
		_activeScreenCaptureForm = screenCaptureForm;
		screenCaptureForm.TopMost = true;
		screenCaptureForm.ShowDialog();
		_activeScreenCaptureForm = null;
	}

	public async Task<OcrResult> TakeScreenshotAndExtractText(OcrReadingOrder ocrReadingOrder)
	{
		if (_activeScreenCaptureForm is not null)
		{
			_activeScreenCaptureForm.Activate();
			return new OcrResult(false, "Screenshot already in progress.");
		}

		if (Screen.PrimaryScreen == null)
			return new OcrResult(false, "No primary screen detected.");

		using Bitmap screenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
		using Graphics g = Graphics.FromImage(screenshot);
		g.CopyFromScreen(0, 0, 0, 0, Screen.PrimaryScreen.Bounds.Size);

		var displayShot = screenshot;
		using Bitmap invertedScreenshot = InvertScreenshotColors(screenshot);
		if (_settings.AzureComputerVisionSettings != null && _settings.AzureComputerVisionSettings.InvertScreenshot)
			displayShot = invertedScreenshot;

		using (ScreenCaptureForm screenCaptureForm = new ScreenCaptureForm(new Bitmap(displayShot)))
		{
			_activeScreenCaptureForm = screenCaptureForm;
			screenCaptureForm.TopMost = true;
			screenCaptureForm.ShowDialog();
			_activeScreenCaptureForm = null;
		}

		return await ExtractTextFromClipboardImage(ocrReadingOrder).ConfigureAwait(true);
	}

	public async Task<OcrResult> ExtractTextFromClipboardImage(OcrReadingOrder ocrReadingOrder)
	{
		if (_ocrState.BusyWithTextExtraction)
		{
			_ocrState.StopTextExtraction();
			return new OcrResult(false, "OCR cancelled by user.");
		}

		var image = await TryGetClipboardImageAsync().ConfigureAwait(true);
		if (image is null)
		{
			string msg = "No image found on the clipboard after multiple retries.";
			SetTextToClipboard(msg);
			BeepPlayer.Play(BeepType.Failure);
			return new OcrResult(false, msg);
		}

		try
		{
			_ocrState.StartTextExtraction();
			return await ExtractTextViaOcr(ocrReadingOrder, image).ConfigureAwait(true);
		}
		finally
		{
			_ocrState.StopTextExtraction();
		}
	}

	private async Task<OcrResult> ExtractTextViaOcr(OcrReadingOrder ocrReadingOrder, Image image)
	{
		if (image is null)
			return new OcrResult(false, "No image provided to perform OCR on.");

		try
		{
			BeepPlayer.Play(BeepType.Start);

			using MemoryStream imageStream = new MemoryStream();
			image.Save(imageStream, ImageFormat.Jpeg);
			imageStream.Seek(0, SeekOrigin.Begin);
			string text = await _ocrService.ExtractText(ocrReadingOrder, imageStream, _ocrState.OcrCancellationTokenSource!.Token).ConfigureAwait(true);

			SetTextToClipboard(text);
			BeepPlayer.Play(BeepType.Success);
			return new OcrResult(true, $"Converted text is on clipboard:{Environment.NewLine}{text}");
		}
		catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			BeepPlayer.Play(BeepType.Failure);
			string msg = "OCR cancelled by user.";
			SetTextToClipboard(msg);
			return new OcrResult(false, msg);
		}
		catch (Exception ex)
		{
			BeepPlayer.Play(BeepType.Failure);
			string msg = $"Failed to extract text via OCR: {ex.Message}{Environment.NewLine}{ex.GetType().FullName}{Environment.NewLine}{ex.StackTrace}";
			SetTextToClipboard(msg);
			return new OcrResult(false, msg);
		}
	}

	public async Task<Image?> TryGetClipboardImageAsync()
	{
		int attempts = 5;
		while (attempts > 0)
		{
			if (Clipboard.ContainsImage())
				return Clipboard.GetImage();

			attempts--;
			await Task.Delay(150).ConfigureAwait(true);
		}
		return null;
	}

	public void SetTextToClipboard(string text)
	{
		if (!string.IsNullOrWhiteSpace(text))
			Clipboard.SetText(text, TextDataFormat.UnicodeText);
	}

	private static Bitmap InvertScreenshotColors(Bitmap original)
	{
		Bitmap inverted = new Bitmap(original.Width, original.Height);
		using Graphics g = Graphics.FromImage(inverted);
		ColorMatrix invertMatrix = new ColorMatrix(new float[][]
		{
				new float[]{ -1,  0,  0, 0, 0 },
				new float[]{  0, -1,  0, 0, 0 },
				new float[]{  0,  0, -1, 0, 0 },
				new float[]{  0,  0,  0, 1, 0 },
				new float[]{  1,  1,  1, 0, 1 }
		});
		using ImageAttributes attributes = new ImageAttributes();
		attributes.SetColorMatrix(invertMatrix);
		g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height), 0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
		return inverted;
	}
}

public record OcrResult(bool Success, string Message);
