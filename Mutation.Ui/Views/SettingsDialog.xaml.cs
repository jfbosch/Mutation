using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using CognitiveSupport;

namespace Mutation.Ui.Views;

public sealed partial class SettingsDialog : ContentDialog
{
	private SettingsCategoryItem? _selectedCategory;
	private readonly Settings _settings;

	public ObservableCollection<SettingsCategoryItem> Categories { get; } = new();

	public SettingsDialog(Settings settings)
	{
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
		AutomationProperties.SetHelpText(CategoryDetails, "Configure settings for this category.");

		// Clear dynamic children (index > 2: Title, Summary, Placeholder)
		// We expect Title (0), Summary (1), Placeholder (2)
		while (CategoryDetails.Children.Count > 3)
		{
			CategoryDetails.Children.RemoveAt(3);
		}

		if (_selectedCategory.Key == "speech")
		{
			if (PlaceholderText != null) PlaceholderText.Visibility = Visibility.Collapsed;

			var panel = new StackPanel { Spacing = 8, Orientation = Orientation.Vertical, Margin = new Thickness(0, 12, 0, 0) };
			panel.Children.Add(new TextBlock
			{
				Text = "File Transcription Timeout (seconds)",
				Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
			});

			var numberBox = new NumberBox
			{
				Value = _settings.SpeechToTextSettings?.FileTranscriptionTimeoutSeconds ?? 300,
				Minimum = 10,
				Maximum = 7200, // 2 hours
				SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
				SmallChange = 10,
				LargeChange = 60,
				Width = 200,
				HorizontalAlignment = HorizontalAlignment.Left
			};

			numberBox.ValueChanged += (s, e) =>
			{
				if (_settings.SpeechToTextSettings != null && !double.IsNaN(e.NewValue))
					_settings.SpeechToTextSettings.FileTranscriptionTimeoutSeconds = (int)e.NewValue;
			};

			panel.Children.Add(numberBox);
			CategoryDetails.Children.Add(panel);
		}
		else
		{
			if (PlaceholderText != null) PlaceholderText.Visibility = Visibility.Visible;
		}
	}

	private void SetDialogSize()
	{
		Loaded += (s, e) =>
		{
			if (XamlRoot is null)
				return;

			var bounds = XamlRoot.Size;
			var targetWidth = Math.Min(1200, bounds.Width * 0.9);
			var targetHeight = Math.Min(800, bounds.Height * 0.9);

			MinWidth = 720;
			MinHeight = 540;
			Width = targetWidth;
			Height = targetHeight;
			MaxWidth = targetWidth;
			MaxHeight = targetHeight;
		};
	}
}
