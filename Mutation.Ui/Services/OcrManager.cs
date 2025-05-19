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
using Mutation.Ui.Views;
using WinRT;
using WinRT.Interop;
using CognitiveSupport;
using Windows.Foundation;

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
        BeepPlayer.Play(BeepType.Start);
        var bitmap = await CaptureScreenshotAsync();
        if (bitmap != null)
        {
            await _clipboard.SetImageAsync(bitmap);
            BeepPlayer.Play(BeepType.Success);
        }
    }

    public async Task<OcrResult> TakeScreenshotAndExtractTextAsync(OcrReadingOrder order)
    {
        BeepPlayer.Play(BeepType.Start);
        var bitmap = await CaptureScreenshotAsync();
        if (bitmap == null)
        {
            BeepPlayer.Play(BeepType.Failure);
            return new(false, "Screenshot cancelled.");
        }

        await _clipboard.SetImageAsync(bitmap);
        var result = await ExtractTextViaOcrAsync(order, bitmap);
        BeepPlayer.Play(result.Success ? BeepType.Success : BeepType.Failure);
        return result;
    }

    public async Task<OcrResult> ExtractTextFromClipboardImageAsync(OcrReadingOrder order)
    {
        var bitmap = await _clipboard.TryGetImageAsync();
        if (bitmap == null)
        {
            BeepPlayer.Play(BeepType.Failure);
            return new(false, "No image on clipboard.");
        }

        var result = await ExtractTextViaOcrAsync(order, bitmap);
        BeepPlayer.Play(result.Success ? BeepType.Success : BeepType.Failure);
        return result;
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
        var bmp = await tcs.Task.ConfigureAwait(false);
        if (bmp == null)
            return null;

        // show region selection overlay
        var overlay = new RegionSelectionWindow();
        await overlay.InitializeAsync(bmp);
        Rect? rect = await overlay.SelectRegionAsync();
        if (rect == null || rect.Value.Width < 1 || rect.Value.Height < 1)
            return null;

        return await CropBitmapAsync(bmp, rect.Value);
    }

    private static async Task<SoftwareBitmap> CropBitmapAsync(SoftwareBitmap src, Rect rect)
    {
        using InMemoryRandomAccessStream stream = new();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(src);
        await encoder.FlushAsync();
        stream.Seek(0);

        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        BitmapBounds bounds = new()
        {
            X = (uint)rect.X,
            Y = (uint)rect.Y,
            Width = (uint)rect.Width,
            Height = (uint)rect.Height
        };
        BitmapTransform transform = new() { Bounds = bounds };
        return await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
    }
}
