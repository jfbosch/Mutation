using CognitiveSupport;
using Windows.Graphics.Capture;

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
        // TODO: implement capture and OCR
        return new OcrResult(false, "Not implemented");
    }
}

public record OcrResult(bool Success, string Message);
