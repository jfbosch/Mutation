using System.Drawing;
using System.Windows.Forms;

namespace Mutation;

/// <summary>
/// Persists and restores window location, size and related UI state.
/// </summary>
public class UiStateManager
{
    private readonly Settings _settings;

    public UiStateManager(Settings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Restores the window location and size from the settings object.
    /// </summary>
    public void Restore(Form form)
    {
        if (form == null)
            throw new ArgumentNullException(nameof(form));

        if (_settings.MainWindowUiSettings == null)
            return;

        if (_settings.MainWindowUiSettings.WindowSize != Size.Empty)
        {
            form.Size = new Size(
                Math.Min(_settings.MainWindowUiSettings.WindowSize.Width, Screen.PrimaryScreen.Bounds.Width),
                Math.Min(_settings.MainWindowUiSettings.WindowSize.Height, Screen.PrimaryScreen.Bounds.Height));
        }

        if (form.Size.Width < 150 || form.Size.Height < 150)
        {
            form.Size = new Size(Math.Max(form.Size.Width, 150), Math.Max(form.Size.Height, 150));
        }

        if (_settings.MainWindowUiSettings.WindowLocation != Point.Empty)
        {
            form.Location = new Point(
                Math.Max(Math.Min(_settings.MainWindowUiSettings.WindowLocation.X, Screen.PrimaryScreen.Bounds.Width - form.Size.Width), 0),
                Math.Max(Math.Min(_settings.MainWindowUiSettings.WindowLocation.Y, Screen.PrimaryScreen.Bounds.Height - form.Size.Height), 0));
        }
    }

    /// <summary>
    /// Saves the window location and size to the settings object.
    /// </summary>
    public void Save(Form form)
    {
        if (form == null)
            throw new ArgumentNullException(nameof(form));

        if (_settings.MainWindowUiSettings == null)
            return;

        _settings.MainWindowUiSettings.WindowSize = form.Size;
        _settings.MainWindowUiSettings.WindowLocation = form.Location;
    }
}
