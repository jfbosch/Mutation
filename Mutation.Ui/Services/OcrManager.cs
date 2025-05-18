using CognitiveSupport;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.System;

namespace Mutation.Ui.Services;

/// <summary>
/// Placeholder OCR manager using WinUI GraphicsCapturePicker.
/// </summary>
public class OcrManager
{
    private readonly Settings _settings;
    private readonly IOcrService _ocrService;
    private readonly ClipboardManager _clipboardManager;

    public OcrManager(Settings settings, IOcrService ocrService, ClipboardManager clipboardManager)
    {
        _settings = settings;
        _ocrService = ocrService;
        _clipboardManager = clipboardManager;
    }

    public async Task<OcrResult> TakeScreenshotAndExtractText(OcrReadingOrder order)
    {
        bool launched = await Launcher.LaunchUriAsync(new Uri("ms-screenclip:"));
        if (!launched)
            return new OcrResult(false, "Failed to launch screen clip tool.");

        var image = await _clipboardManager.TryGetImageAsync(20, 300);
        if (image is null)
            return new OcrResult(false, "No image captured.");

        return await ExtractTextFromBitmap(order, image);
    }

    public async Task<OcrResult> ExtractTextFromClipboardImage(OcrReadingOrder order)
    {
        var image = await _clipboardManager.TryGetImageAsync();
        if (image is null)
        {
            const string msg = "No image found on the clipboard.";
            _clipboardManager.SetText(msg);
            return new OcrResult(false, msg);
        }

        return await ExtractTextFromBitmap(order, image);
    }

    private async Task<OcrResult> ExtractTextFromBitmap(OcrReadingOrder order, SoftwareBitmap bitmap)
    {
        try
        {
            using InMemoryRandomAccessStream mem = new();
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, mem);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
            mem.Seek(0);

            using var stream = mem.AsStream();
            string text = await _ocrService.ExtractText(order, stream, CancellationToken.None);
            _clipboardManager.SetText(text);
            return new OcrResult(true, $"Converted text is on clipboard:{Environment.NewLine}{text}");
        }
        catch (Exception ex)
        {
            string msg = $"Failed to extract text via OCR: {ex.Message}";
            _clipboardManager.SetText(msg);
            return new OcrResult(false, msg);
        }
    }
}

public record OcrResult(bool Success, string Message);
