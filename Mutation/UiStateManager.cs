using CognitiveSupport;

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
		var mainWindowUiSettings = _settings.MainWindowUiSettings;

		if (mainWindowUiSettings.WindowSize != Size.Empty)
		{
			form.Size = new Size(
				 Math.Min(mainWindowUiSettings.WindowSize.Width, Screen.PrimaryScreen?.Bounds.Width ?? form.Size.Width),
				 Math.Min(mainWindowUiSettings.WindowSize.Height, Screen.PrimaryScreen?.Bounds.Height ?? form.Size.Height));
		}

		if (form.Size.Width < 150 || form.Size.Height < 150)
		{
			form.Size = new Size(Math.Max(form.Size.Width, 150), Math.Max(form.Size.Height, 150));
		}

		if (mainWindowUiSettings.WindowLocation != Point.Empty)
		{
			form.Location = new Point(
				 Math.Max(Math.Min(mainWindowUiSettings.WindowLocation.X, (Screen.PrimaryScreen?.Bounds.Width ?? form.Size.Width) - form.Size.Width), 0),
				 Math.Max(Math.Min(mainWindowUiSettings.WindowLocation.Y, (Screen.PrimaryScreen?.Bounds.Height ?? form.Size.Height) - form.Size.Height), 0));
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
