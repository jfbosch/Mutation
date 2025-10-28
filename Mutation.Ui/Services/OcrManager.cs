using CognitiveSupport;
using Microsoft.UI.Xaml;
using Mutation.Ui.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
public record OcrBatchResult(bool Success, string Text, int TotalCount, int SuccessCount, IReadOnlyList<string> Failures);

public class OcrManager
{
    private readonly Settings _settings;
    private readonly IOcrService _ocrService;
    private readonly ClipboardManager _clipboard;
    private Window? _window;
    private RegionSelectionWindow? _activeOverlay;
    private RegionSelectionWindow? _cachedOverlay;

    public OcrManager(Settings settings, IOcrService ocrService, ClipboardManager clipboard)
    {
        _settings = settings;
        _ocrService = ocrService;
        _clipboard = clipboard;
    }

    public void InitializeWindow(Window window)
    {
        _window = window;
        // Pre-warm a reusable overlay instance to reduce first-use latency
        try { _cachedOverlay = new RegionSelectionWindow(); _cachedOverlay.PrepareWindowForReuse(); } catch { _cachedOverlay = null; }
    }

    public async Task TakeScreenshotToClipboardAsync()
    {
        if (_activeOverlay is not null)
        {
            try { _activeOverlay.BringToFront(); } catch { }
            return;
        }
        var bitmap = await CaptureScreenshotAsync();
        if (bitmap != null)
        {
            await _clipboard.SetImageAsync(bitmap);
            _ = Task.Run(() => PlayBeep(BeepType.Success));
        }
    }

    public async Task<OcrResult> TakeScreenshotAndExtractTextAsync(OcrReadingOrder order)
    {
        if (_activeOverlay is not null)
        {
            try { _activeOverlay.BringToFront(); } catch { }
            return new(false, "Screenshot already in progress");
        }
        var bitmap = await CaptureScreenshotAsync();
        if (bitmap == null)
        {
            _ = Task.Run(() => PlayBeep(BeepType.Failure));
            return new(false, "Screenshot cancelled.");
        }

        await _clipboard.SetImageAsync(bitmap);
        var result = await ExtractTextViaOcrAsync(order, bitmap);
        _ = Task.Run(() => PlayBeep(result.Success ? BeepType.Success : BeepType.Failure));
        return result;
    }

    public async Task<OcrResult> ExtractTextFromClipboardImageAsync(OcrReadingOrder order)
    {
        var bitmap = await _clipboard.TryGetImageAsync();
        if (bitmap == null)
        {
            PlayBeep(BeepType.Failure);
            return new(false, "No image on clipboard.");
        }

        var result = await ExtractTextViaOcrAsync(order, bitmap);
        _ = Task.Run(() => PlayBeep(result.Success ? BeepType.Success : BeepType.Failure));
        return result;
    }

    public async Task<OcrBatchResult> ExtractTextFromFilesAsync(IEnumerable<string> filePaths, OcrReadingOrder order, CancellationToken cancellationToken)
    {
        if (filePaths is null)
            throw new ArgumentNullException(nameof(filePaths));

        List<string> paths = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (paths.Count == 0)
            return new(false, string.Empty, 0, 0, Array.Empty<string>());

        var combinedText = new StringBuilder();
        var failures = new List<string>();
        int successCount = 0;

        foreach (string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var stream = File.OpenRead(path);
                string text = await _ocrService.ExtractText(order, stream, cancellationToken);

                if (combinedText.Length > 0)
                {
                    combinedText.AppendLine().AppendLine();
                }

                string fileName = Path.GetFileName(path);
                combinedText.AppendLine($"[{fileName}]");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    combinedText.AppendLine(text.TrimEnd());
                }

                successCount++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                string fileName = Path.GetFileName(path);
                failures.Add($"{fileName}: {ex.Message}");
            }
        }

        string resultText = combinedText.ToString();
        if (successCount > 0 && !string.IsNullOrWhiteSpace(resultText))
        {
            await SetClipboardTextAsync(resultText);
        }

        bool success = successCount > 0 && failures.Count == 0;
        _ = Task.Run(() => PlayBeep(success ? BeepType.Success : BeepType.Failure));

        return new(success, resultText, paths.Count, successCount, failures.AsReadOnly());
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
            await SetClipboardTextAsync(text);
            return new(true, text);
        }
        catch (Exception ex)
        {
            return new(false, ex.Message);
        }
    }

    private async Task SetClipboardTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (HasDispatcherThreadAccess())
        {
            _clipboard.SetText(text);
            return;
        }

        await RunOnDispatcherAsync(() => _clipboard.SetText(text));
    }

    protected virtual bool HasDispatcherThreadAccess()
    {
        var dispatcher = _window?.DispatcherQueue;
        return dispatcher?.HasThreadAccess ?? true;
    }

    protected virtual Task RunOnDispatcherAsync(Action action)
    {
        var dispatcher = _window?.DispatcherQueue;
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<object?>();
        if (!dispatcher.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }))
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue work on the dispatcher."));
        }

        return tcs.Task;
    }

    protected virtual void PlayBeep(BeepType type)
    {
        BeepPlayer.Play(type);
    }

    private async Task<SoftwareBitmap?> CaptureScreenshotAsync()
    {
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

        var overlay = _cachedOverlay ?? new RegionSelectionWindow();
        await overlay.InitializeAsync(bmp);
        overlay.UpdateBitmap(bmp);
        _activeOverlay = overlay;
        try
        {
            // Activate and show overlay (inside SelectRegionAsync), then play start beep asynchronously to avoid UI delay
            var selectTask = overlay.SelectRegionAsync();
            _ = Task.Run(() => PlayBeep(BeepType.Start));
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
