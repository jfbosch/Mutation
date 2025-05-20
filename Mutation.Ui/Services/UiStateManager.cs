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
if (appWindow == null || ui == null)
return;

// Get display area for the window
var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
var bounds = displayArea.WorkArea;

// Minimum window size
const int minWidth = 150;
const int minHeight = 150;

// Clamp size
int width = Math.Max(minWidth, Math.Min(ui.WindowSize.Width, bounds.Width));
int height = Math.Max(minHeight, Math.Min(ui.WindowSize.Height, bounds.Height));

// Clamp position
int x = Math.Max(bounds.X, Math.Min(ui.WindowLocation.X, bounds.X + bounds.Width - width));
int y = Math.Max(bounds.Y, Math.Min(ui.WindowLocation.Y, bounds.Y + bounds.Height - height));

// Apply size and position
appWindow.Resize(new SizeInt32(width, height));
appWindow.Move(new PointInt32(x, y));
}

public void Save(Window window)
{
if (window == null) throw new ArgumentNullException(nameof(window));
var appWindow = window.AppWindow;
_settings.MainWindowUiSettings.WindowSize = new System.Drawing.Size(appWindow.Size.Width, appWindow.Size.Height);
_settings.MainWindowUiSettings.WindowLocation = new System.Drawing.Point(appWindow.Position.X, appWindow.Position.Y);
}
}
