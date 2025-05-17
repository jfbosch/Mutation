using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Mutation;

/// <summary>
/// Encapsulates clipboard interactions for text and images.
/// </summary>
public class ClipboardManager
{
        /// <summary>
        /// Attempts to get an image from the clipboard with retries.
        /// </summary>
        public async Task<Image?> TryGetImageAsync(int attempts = 5, int delayMs = 150)
        {
                while (attempts > 0)
                {
                        if (Clipboard.ContainsImage())
                                return Clipboard.GetImage();

                        attempts--;
                        await Task.Delay(delayMs).ConfigureAwait(true);
                }
                return null;
        }

        /// <summary>
        /// Sets text to the clipboard if not empty.
        /// </summary>
        public void SetText(string text)
        {
                if (!string.IsNullOrWhiteSpace(text))
                        Clipboard.SetText(text, TextDataFormat.UnicodeText);
        }

        /// <summary>
        /// Gets text from the clipboard.
        /// </summary>
        public string GetText() => Clipboard.GetText();

        /// <summary>
        /// Places the provided image on the clipboard.
        /// </summary>
        public void SetImage(Image image)
        {
                Clipboard.SetImage(image);
        }
}
