using CognitiveSupport;
using Microsoft.UI.Xaml;
using Mutation.Ui.Views;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices.WindowsRuntime;

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
    private RegionSelectionWindow? _activeOverlay;

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
        // If an overlay is already active, just bring it to front and return (no beep)
        if (_activeOverlay is not null)
        {
            try { _activeOverlay.BringToFront(); } catch { }
            return;
        }
        var bitmap = await CaptureScreenshotAsync();
        if (bitmap != null)
        {
            await _clipboard.SetImageAsync(bitmap);
            _ = Task.Run(() => BeepPlayer.Play(BeepType.Success));
        }
    }

    public async Task<OcrResult> TakeScreenshotAndExtractTextAsync(OcrReadingOrder order)
    {
        // If an overlay is already active, focus it and return a neutral result (no beep)
        if (_activeOverlay is not null)
        {
            try { _activeOverlay.BringToFront(); } catch { }
            return new(false, "Screenshot already in progress");
        }
        var bitmap = await CaptureScreenshotAsync();
        if (bitmap == null)
        {
            _ = Task.Run(() => BeepPlayer.Play(BeepType.Failure));
            return new(false, "Screenshot cancelled.");
        }

        await _clipboard.SetImageAsync(bitmap);
        var result = await ExtractTextViaOcrAsync(order, bitmap);
        _ = Task.Run(() => BeepPlayer.Play(result.Success ? BeepType.Success : BeepType.Failure));
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
        _ = Task.Run(() => BeepPlayer.Play(result.Success ? BeepType.Success : BeepType.Failure));
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
        // Capture the entire virtual screen using GDI and convert to SoftwareBitmap
        // Minimize chance of flicker by temporarily hiding our window during capture
        var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
        FormWindowState? prevState = null;
        IntPtr? hwnd = null;
        try
        {
            if (_window is not null)
            {
                hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            }
        }
        catch { }

        using Bitmap gdiBmp = new(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(gdiBmp))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
        }

    // Fast path: copy GDI pixels directly into a SoftwareBitmap without PNG encode/decode
    SoftwareBitmap bmp;
    var gdiRect = new Rectangle(0, 0, gdiBmp.Width, gdiBmp.Height);
    var data = gdiBmp.LockBits(gdiRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            int srcStride = data.Stride;
            int height = data.Height;
            int width = data.Width;
            int length = Math.Abs(srcStride) * height;
            byte[] pixels = new byte[length];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, length);
            var ibuffer = pixels.AsBuffer();
            bmp = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
            bmp.CopyFromBuffer(ibuffer);
        }
        finally
        {
            gdiBmp.UnlockBits(data);
        }

    // show region selection overlay (deferred activate to avoid flash)
        var overlay = new RegionSelectionWindow();
        await overlay.InitializeAsync(bmp);
        _activeOverlay = overlay;
        try
        {
            // Activate and show overlay (inside SelectRegionAsync), then play start beep asynchronously to avoid UI delay
            var selectTask = overlay.SelectRegionAsync();
            _ = Task.Run(() => BeepPlayer.Play(BeepType.Start));
            Rect? selectionRect = await selectTask;
            if (selectionRect == null || selectionRect.Value.Width < 1 || selectionRect.Value.Height < 1)
                return null;
            return await CropBitmapAsync(bmp, selectionRect.Value);
        }
        finally
        {
            _activeOverlay = null;
        }
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
