namespace Mutation.Ui.Views;

public sealed class SettingsCategoryItem
{
	public SettingsCategoryItem(string key, string displayName, string description, string iconGlyph)
	{
		Key = key;
		DisplayName = displayName;
		Description = description;
		IconGlyph = iconGlyph;
	}

	public string Key { get; }
	public string DisplayName { get; }
	public string Description { get; }
	public string IconGlyph { get; }
}