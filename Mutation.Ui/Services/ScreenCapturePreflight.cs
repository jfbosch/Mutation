using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Mutation.Ui.Services
{
    internal static class ScreenCapturePreflight
    {
        public static (bool ok, string? message) TryCaptureProbe()
        {
            try
            {
                var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
                using Bitmap gdiBmp = new(2, 2, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(gdiBmp))
                {
                    g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new System.Drawing.Size(2, 2));
                }

                using MemoryStream ms = new();
                gdiBmp.Save(ms, ImageFormat.Png); // encode to ensure pixels are accessible
                return (true, null);
            }
            catch (Exception ex)
            {
                string hint = "Screen capture appears to be blocked. If you're on a corporate or restricted device, a group policy may disable screen capture. " +
                              "Also ensure no privacy tools are blocking screenshots and that you are not capturing protected windows (e.g., DRM content).\n\n" +
                              $"Details: {ex.Message}";
                return (false, hint);
            }
        }
    }
}
