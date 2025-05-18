using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Mutation.Ui.Services;
using WinRT;
using WinRT.Interop;
using CognitiveSupport;

namespace Mutation.Ui.Services;

public record OcrResult(bool Success, string Message);

/// <summary>
/// Handles screenshot capturing and OCR for WinUI.
/// </summary>
public class OcrManager
{
    private readonly Settings _settings;
    private readonly IOcrService _ocrService;
    private readonly ClipboardManager _clipboard;
    private Window? _window;

    public OcrManager(Settings settings, IOcrService ocrService, ClipboardManager clipboard)
    {
        _settings = settings;
        _ocrService = ocrService;
        _clipboard = clipboard;
    }

    public void InitializeWindow(Window window)
    {
        _window = window;
    }

    public async Task TakeScreenshotToClipboardAsync()
    {
        var bitmap = await CaptureScreenshotAsync();
        if (bitmap != null)
            await _clipboard.SetImageAsync(bitmap);
    }

    public async Task<OcrResult> TakeScreenshotAndExtractTextAsync(OcrReadingOrder order)
    {
        var bitmap = await CaptureScreenshotAsync();
        if (bitmap == null)
            return new(false, "Screenshot cancelled.");

        await _clipboard.SetImageAsync(bitmap);
        return await ExtractTextViaOcrAsync(order, bitmap);
    }

    public async Task<OcrResult> ExtractTextFromClipboardImageAsync(OcrReadingOrder order)
    {
        var bitmap = await _clipboard.TryGetImageAsync();
        if (bitmap == null)
            return new(false, "No image on clipboard.");

        return await ExtractTextViaOcrAsync(order, bitmap);
    }

    private async Task<OcrResult> ExtractTextViaOcrAsync(OcrReadingOrder order, SoftwareBitmap bitmap)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        stream.Seek(0);

        using Stream netStream = stream.AsStream();
        try
        {
            var text = await _ocrService.ExtractText(order, netStream, default);
            _clipboard.SetText(text);
            return new(true, text);
        }
        catch (Exception ex)
        {
            return new(false, ex.Message);
        }
    }

    private async Task<SoftwareBitmap?> CaptureScreenshotAsync()
    {
        if (_window == null)
            throw new InvalidOperationException("OcrManager window not initialized.");

        var picker = new GraphicsCapturePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
        GraphicsCaptureItem item = await picker.PickSingleItemAsync();
        if (item == null)
            return null;

        var device = Direct3D11Helper.CreateDevice();
        using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size);
        using var session = framePool.CreateCaptureSession(item);
        var tcs = new TaskCompletionSource<SoftwareBitmap?>();
        void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            using var frame = sender.TryGetNextFrame();
            sender.FrameArrived -= OnFrameArrived;
            session.Dispose();
            var task = SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
            task.AsTask().ContinueWith(t => tcs.SetResult(t.Result));
        }
        framePool.FrameArrived += OnFrameArrived;
        session.StartCapture();
        return await tcs.Task.ConfigureAwait(false);
    }
}
