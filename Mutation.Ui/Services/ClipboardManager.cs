using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Mutation.Ui.Services;

/// <summary>
/// Clipboard helper for WinUI.  This intentionally avoids System.Windows.Forms.
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
                var reference = await content.GetBitmapAsync();
                IRandomAccessStream stream = await reference.OpenReadAsync();
                return await SoftwareBitmapDecoder.CreateAsync(stream).AsTask().ContinueWith(t => t.Result.SoftwareBitmap);
            }
            await Task.Delay(delayMs);
        }
        return null;
    }

    public void SetText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    public async Task<string> GetTextAsync()
    {
        var content = Clipboard.GetContent();
        return await content.GetTextAsync();
    }
}
