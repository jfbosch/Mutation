using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Mutation.Ui.Views;

public sealed partial class SettingsDialog : ContentDialog
{
	private SettingsCategoryItem? _selectedCategory;

	public ObservableCollection<SettingsCategoryItem> Categories { get; } = new();

	public SettingsDialog()
	{
		PopulateCategories();
		InitializeComponent();
		SetDialogSize();

		if (Categories.Count > 0)
		{
			_selectedCategory = Categories[0];
			UpdateCategoryDetails();
			CategoryList.SelectedIndex = 0;
		}
		else
		{
			UpdateCategoryDetails();
		}
	}

	private void PopulateCategories()
	{
		Categories.Clear();
		Categories.Add(new SettingsCategoryItem("general", "General", "High-level workspace preferences and default instructions.", "\uE713"));
		Categories.Add(new SettingsCategoryItem("audio", "Audio", "Configure microphones, mute hotkeys, and capture feedback.", "\uE720"));
		Categories.Add(new SettingsCategoryItem("ocr", "Screen capture & OCR", "Manage screenshot automation, inversion, and recognition workflows.", "\uE8C8"));
		Categories.Add(new SettingsCategoryItem("speech", "Speech to Text", "Choose transcription providers and tailor recording behavior.", "\uE7C8"));
		Categories.Add(new SettingsCategoryItem("llm", "AI assistance", "Connect to large language models and manage formatting prompts.", "\uE756"));
		Categories.Add(new SettingsCategoryItem("tts", "Text to Speech", "Personalize voice playback and narration options.", "\uE8D2"));
		Categories.Add(new SettingsCategoryItem("ui", "Interface", "Adjust layout preferences, window size, and transcript limits.", "\uE771"));
		Categories.Add(new SettingsCategoryItem("hotkeys", "Hotkeys", "Review and customize global shortcuts for faster actions.", "\uE765"));
	}

	private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		_selectedCategory = CategoryList.SelectedItem as SettingsCategoryItem;
		UpdateCategoryDetails();
	}

	private void UpdateCategoryDetails()
	{
		if (_selectedCategory is null)
		{
			CategoryTitle.Text = "Select a category";
			CategorySummary.Text = "Choose a settings category from the list to preview its configuration area.";
			AutomationProperties.SetName(CategoryDetails, "Settings preview");
			AutomationProperties.SetHelpText(CategoryDetails, CategorySummary.Text);
			return;
		}

		CategoryTitle.Text = _selectedCategory.DisplayName;
		CategorySummary.Text = _selectedCategory.Description;
		AutomationProperties.SetName(CategoryDetails, _selectedCategory.DisplayName);
		AutomationProperties.SetHelpText(CategoryDetails, "Configuration options will appear here in a future update.");
	}

	private void SetDialogSize()
	{
		Loaded += (s, e) =>
		{
			if (XamlRoot is null)
				return;

			var bounds = XamlRoot.Size;
			MaxWidth = bounds.Width * 0.5;
			MaxHeight = bounds.Height * 0.5;
		};
	}

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
}
