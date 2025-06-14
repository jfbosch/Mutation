using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace Mutation.Ui.Services;

/// <summary>
/// Provides clipboard helpers for WinUI.
/// </summary>
public class ClipboardManager
{
public async Task<SoftwareBitmap?> TryGetImageAsync(int attempts = 5, int delayMs = 150)
{
while (attempts-- > 0)
{
var content = Clipboard.GetContent();
if (content.Contains(StandardDataFormats.Bitmap))
{
IRandomAccessStreamReference? streamRef = await content.GetBitmapAsync();
if (streamRef != null)
{
using var stream = await streamRef.OpenReadAsync();
var decoder = await BitmapDecoder.CreateAsync(stream);
return await decoder.GetSoftwareBitmapAsync();
}
}

await Task.Delay(delayMs);
}
return null;
}

public void SetText(string text)
{
if (string.IsNullOrWhiteSpace(text))
return;

var data = new DataPackage();
data.SetText(text);
Clipboard.SetContent(data);
}

public async Task<string> GetTextAsync()
{
    var content = Clipboard.GetContent();
    return content.Contains(StandardDataFormats.Text)
        ? await content.GetTextAsync().AsTask()
        : string.Empty;
}

public async Task SetImageAsync(SoftwareBitmap bitmap)
{
var data = new DataPackage();
var stream = new InMemoryRandomAccessStream();
var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
encoder.SetSoftwareBitmap(bitmap);
await encoder.FlushAsync();
stream.Seek(0);
data.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
Clipboard.SetContent(data);
}
}
