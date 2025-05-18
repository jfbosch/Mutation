using System.Drawing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Mutation.Ui.Services;
using CognitiveSupport;

namespace Mutation.Ui.Services;

/// <summary>
/// Persists and restores window size and location using WinUI APIs.
/// </summary>
public class UiStateManager
{
    private readonly Settings _settings;
    public UiStateManager(Settings settings)
    {
        _settings = settings;
    }

    public void Restore(Window window)
    {
        if (_settings.MainWindowUiSettings == null)
            return;

        var appWindow = GetAppWindow(window);
        var size = _settings.MainWindowUiSettings.WindowSize;
        if (size != Size.Empty)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(size.Width, size.Height));
        }
        var loc = _settings.MainWindowUiSettings.WindowLocation;
        if (loc != Point.Empty)
        {
            appWindow.Move(new Windows.Graphics.PointInt32(loc.X, loc.Y));
        }
    }

    public void Save(Window window)
    {
        if (_settings.MainWindowUiSettings == null)
            return;

        var appWindow = GetAppWindow(window);
        var pos = appWindow.Position;
        var size = appWindow.Size;
        _settings.MainWindowUiSettings.WindowLocation = new Point(pos.X, pos.Y);
        _settings.MainWindowUiSettings.WindowSize = new Size(size.Width, size.Height);
    }

    private static AppWindow GetAppWindow(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(id);
    }
}
