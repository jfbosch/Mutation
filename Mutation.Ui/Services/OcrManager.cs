﻿using CognitiveSupport;
using Microsoft.UI.Xaml;
using Mutation.Ui.Views;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
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
public record OcrProcessingProgress(int ProcessedSegments, int TotalSegments, string FileName, int PageNumber, int TotalPagesForFile);

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

    public async Task<OcrBatchResult> ExtractTextFromFilesAsync(IEnumerable<string> filePaths, OcrReadingOrder order, CancellationToken cancellationToken, IProgress<OcrProcessingProgress>? progress = null)
    {
        if (filePaths is null)
            throw new ArgumentNullException(nameof(filePaths));

        List<string> paths = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (paths.Count == 0)
            return new(false, string.Empty, 0, 0, Array.Empty<string>());

        if (!IsOcrConfigured(out string configurationError))
        {
            PlayBeep(BeepType.Failure);
            return new(false, string.Empty, paths.Count, 0, new[] { configurationError });
        }

        var batches = ExpandFileBatches(paths);
        int totalSegments = batches.Sum(batch => batch.Items.Count);
        if (totalSegments == 0)
        {
            _ = Task.Run(() => PlayBeep(BeepType.Failure));
            return new(false, string.Empty, paths.Count, 0, Array.Empty<string>());
        }

        var combinedText = new StringBuilder();
        var failures = new List<string>();
        int successCount = 0;
        int processedSegments = 0;

        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool fileHasSuccess = false;
            bool fileHasFailure = false;
            var fileTextBuilder = new StringBuilder();

            foreach (var item in batch.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (item.InitializationError is not null)
                    {
                        fileHasFailure = true;
                        failures.Add($"{batch.FileName}: {item.InitializationError.Message}");
                        continue;
                    }

                    using var stream = item.OpenStream();
                    string text = await _ocrService.ExtractText(order, stream, cancellationToken);

                    if (fileTextBuilder.Length > 0)
                        fileTextBuilder.AppendLine();

                    if (item.TotalPages > 1)
                        fileTextBuilder.AppendLine($"(Page {item.PageNumber})");

                    if (!string.IsNullOrWhiteSpace(text))
                        fileTextBuilder.AppendLine(text.TrimEnd());

                    fileHasSuccess = true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    fileHasFailure = true;
                    failures.Add($"{batch.FileName} (Page {item.PageNumber}): {ex.Message}");
                }
                finally
                {
                    processedSegments++;
                    progress?.Report(new OcrProcessingProgress(processedSegments, totalSegments, batch.FileName, item.PageNumber, item.TotalPages));
                }
            }

            if (fileHasSuccess)
            {
                if (combinedText.Length > 0)
                    combinedText.AppendLine().AppendLine();

                combinedText.AppendLine($"[{batch.FileName}]");

                string fileText = fileTextBuilder.ToString().TrimEnd();
                if (!string.IsNullOrWhiteSpace(fileText))
                    combinedText.AppendLine(fileText);
            }

            if (fileHasSuccess && !fileHasFailure)
                successCount++;
        }

        string resultText = combinedText.ToString();
        if (successCount > 0 && !string.IsNullOrWhiteSpace(resultText))
            await SetClipboardTextAsync(resultText);

        bool success = successCount > 0 && failures.Count == 0;
        _ = Task.Run(() => PlayBeep(success ? BeepType.Success : BeepType.Failure));

        return new(success, resultText, paths.Count, successCount, failures.AsReadOnly());
    }

    private async Task<OcrResult> ExtractTextViaOcrAsync(OcrReadingOrder order, SoftwareBitmap bitmap)
    {
        if (!IsOcrConfigured(out string configurationError))
            return new(false, configurationError);

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

    private bool IsOcrConfigured(out string message)
    {
        var settings = _settings.AzureComputerVisionSettings;
        if (settings is null)
        {
            message = "Azure Computer Vision settings are missing. Update AzureComputerVisionSettings in the settings file.";
            return false;
        }

        bool apiKeyMissing = IsPlaceholderValue(settings.ApiKey);
        bool endpointMissing = IsPlaceholderEndpoint(settings.Endpoint);

        if (!apiKeyMissing && !endpointMissing)
        {
            message = string.Empty;
            return true;
        }

        if (apiKeyMissing && endpointMissing)
        {
            message = "Azure Computer Vision endpoint and API key are not configured. Update AzureComputerVisionSettings in the settings file.";
        }
        else if (apiKeyMissing)
        {
            message = "Azure Computer Vision API key is not configured. Update AzureComputerVisionSettings.ApiKey in the settings file.";
        }
        else
        {
            message = "Azure Computer Vision endpoint is not configured. Update AzureComputerVisionSettings.Endpoint in the settings file.";
        }

        return false;
    }

    private static bool IsPlaceholderValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        string trimmed = value.Trim();
        return string.Equals(trimmed, "<placeholder>", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlaceholderEndpoint(string? endpoint)
    {
        if (IsPlaceholderValue(endpoint))
            return true;

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return true;

        return string.Equals(uri.Host, "placeholder.com", StringComparison.OrdinalIgnoreCase);
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

    private static IReadOnlyList<FileOcrBatch> ExpandFileBatches(IReadOnlyList<string> paths)
    {
        var batches = new List<FileOcrBatch>(paths.Count);

        foreach (string path in paths)
        {
            var items = ExpandFile(path);
            batches.Add(new FileOcrBatch(path, items));
        }

        return batches;
    }

    private static List<OcrWorkItem> ExpandFile(string path)
    {
        var items = new List<OcrWorkItem>();

        if (IsPdf(path))
        {
            try
            {
                using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);

                if (document.PageCount == 0)
                {
                    items.Add(OcrWorkItem.CreateError(path, new InvalidOperationException("PDF contains no pages.")));
                }
                else
                {
                    int totalPages = document.PageCount;

                    for (int i = 0; i < totalPages; i++)
                    {
                        int pageNumber = i + 1;
                        items.Add(OcrWorkItem.CreatePdf(path, pageNumber, totalPages));
                    }
                }
            }
            catch (Exception ex)
            {
                items.Add(OcrWorkItem.CreateError(path, ex));
            }
        }
        else
        {
            items.Add(OcrWorkItem.CreateFile(path));
        }

        return items;
    }

    private static bool IsPdf(string path)
    {
        string extension = Path.GetExtension(path);
        return string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static Stream CreatePdfPageStream(string path, int pageIndex)
    {
        var output = new MemoryStream();

        using (var document = PdfReader.Open(path, PdfDocumentOpenMode.Import))
        {
            if (pageIndex < 0 || pageIndex >= document.PageCount)
                throw new ArgumentOutOfRangeException(nameof(pageIndex));

            using var singlePage = new PdfDocument
            {
                Version = document.Version
            };

            singlePage.Info.Title = document.Info.Title;
            singlePage.Info.Author = document.Info.Author;
            singlePage.Info.Subject = document.Info.Subject;
            singlePage.Info.Keywords = document.Info.Keywords;
            singlePage.AddPage(document.Pages[pageIndex]);
            singlePage.Save(output, false);
        }

        output.Seek(0, SeekOrigin.Begin);
        return output;
    }

    private sealed class FileOcrBatch
    {
        public FileOcrBatch(string path, List<OcrWorkItem> items)
        {
            OriginalPath = path;
            FileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(FileName))
                FileName = path;
            Items = items ?? new List<OcrWorkItem>();
        }

        public string OriginalPath { get; }
        public string FileName { get; }
        public List<OcrWorkItem> Items { get; }
    }

    private sealed class OcrWorkItem
    {
        private readonly Func<Stream>? _streamFactory;

        private OcrWorkItem(string originalPath, Func<Stream>? streamFactory, int pageNumber, int totalPages, Exception? initializationError)
        {
            OriginalPath = originalPath;
            _streamFactory = streamFactory;
            PageNumber = pageNumber;
            TotalPages = totalPages;
            InitializationError = initializationError;
        }

        public string OriginalPath { get; }
        public int PageNumber { get; }
        public int TotalPages { get; }
        public Exception? InitializationError { get; }

        public static OcrWorkItem CreateFile(string path) =>
            new(path, () => File.OpenRead(path), 1, 1, null);

        public static OcrWorkItem CreatePdf(string path, int pageNumber, int totalPages) =>
            new(path, () => CreatePdfPageStream(path, pageNumber - 1), pageNumber, totalPages, null);

        public static OcrWorkItem CreateError(string path, Exception error) =>
            new(path, null, 1, 1, error);

        public Stream OpenStream()
        {
            if (_streamFactory is null)
                throw new InvalidOperationException("No stream factory available.");

            return _streamFactory();
        }
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
