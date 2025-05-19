using System;
using System.Threading.Tasks;
using Windows.Storage;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using CognitiveSupport;
using Windows.Graphics;

namespace Mutation.Ui.Services;

/// <summary>
/// Persists and restores the main window size and position for WinUI.
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
if (window == null) throw new ArgumentNullException(nameof(window));
var appWindow = window.AppWindow;
var ui = _settings.MainWindowUiSettings;
if (ui.WindowSize.Width > 0 && ui.WindowSize.Height > 0)
{
appWindow.Resize(new SizeInt32(ui.WindowSize.Width, ui.WindowSize.Height));
}
if (ui.WindowLocation.X >= 0 && ui.WindowLocation.Y >= 0)
{
appWindow.Move(new PointInt32(ui.WindowLocation.X, ui.WindowLocation.Y));
}
}

public void Save(Window window)
{
if (window == null) throw new ArgumentNullException(nameof(window));
var appWindow = window.AppWindow;
_settings.MainWindowUiSettings.WindowSize = new System.Drawing.Size(appWindow.Size.Width, appWindow.Size.Height);
_settings.MainWindowUiSettings.WindowLocation = new System.Drawing.Point(appWindow.Position.X, appWindow.Position.Y);
}
}
