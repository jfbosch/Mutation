using CognitiveSupport;
using CoreAudio;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Mutation.Ui.Services;
using Mutation.Ui.Views.Settings;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Mutation.Ui.Views;

public sealed partial class SettingsWindow : ContentDialog
{
    private readonly MainWindow _owner;
    private readonly DispatcherQueue _dispatcher;
    private SettingsDialogSaveResult? _pendingSaveResult;
    private SettingsDialogCloseReason _lastCloseReason = SettingsDialogCloseReason.Cancel;

    public SettingsDialogViewModel ViewModel { get; }

    public bool IsShowing { get; private set; }

    public SettingsWindow(MainWindow owner, Settings settings, ISettingsManager settingsManager, AudioDeviceManager audioDeviceManager)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        ViewModel = new SettingsDialogViewModel(settings, settingsManager, audioDeviceManager)
        {
            BrowseForFileAsync = BrowseForBeepAsync
        };

        InitializeComponent();
        DataContext = ViewModel;

        AutomationProperties.SetName(this, "Application settings dialog");
        AutomationProperties.SetHelpText(this, "Configure Mutation preferences.");

        ViewModel.RequestClose += ViewModel_RequestClose;
        ViewModel.FocusRowRequested += ViewModel_FocusRowRequested;

        Opened += SettingsWindow_Opened;
        Closed += SettingsWindow_Closed;
    }

    private void SettingsWindow_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        IsShowing = true;
        _lastCloseReason = SettingsDialogCloseReason.Cancel;
        _ = _dispatcher.TryEnqueue(() => FocusFirstEditor());
    }

    private void SettingsWindow_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        IsShowing = false;
        ViewModel.HandleDialogClosed(_lastCloseReason);
        if (_pendingSaveResult is not null && _lastCloseReason == SettingsDialogCloseReason.Save)
        {
            ApplySaveResult(_pendingSaveResult);
        }
        _pendingSaveResult = null;
    }

    private void ViewModel_RequestClose(object? sender, SettingsDialogCloseRequestEventArgs e)
    {
        _lastCloseReason = e.Reason;
        _pendingSaveResult = e.SaveResult;
        Hide();
    }

    private void ViewModel_FocusRowRequested(object? sender, SettingRowViewModel e)
    {
        if (e is null)
            return;

        SettingsList.ScrollIntoView(e);
        _dispatcher.TryEnqueue(() =>
        {
            var container = SettingsList.ContainerFromItem(e) as ListViewItem;
            if (container is null)
                return;
            var presenter = FindDescendant<FrameworkElement>(container, "EditorPresenter");
            Control? editor = presenter is null ? null : FindDescendant<Control>(presenter);
            (editor ?? (Control?)container)?.Focus(FocusState.Programmatic);
        });
    }

    private void ApplySaveResult(SettingsDialogSaveResult result)
    {
        if (result.ApplyMultiLinePreferences)
        {
            _owner.ApplyMultiLinePreferencesFromSettings();
        }
        if (result.RefreshHotkeyVisuals)
        {
            _owner.RefreshHotkeyVisualsFromSettings();
        }
        if (result.RefreshMicrophoneSelection)
        {
            _owner.RefreshMicrophoneSelectionFromSettings(result.ActiveMicrophoneName);
        }
        if (result.ReloadBeeps)
        {
            BeepPlayer.Initialize(_owner.Settings);
        }
        if (result.ResetWindowPosition)
        {
            _owner.CenterWindowOnCurrentDisplay();
        }
        _owner.ShowTransientStatus("Settings", "Settings saved.", InfoBarSeverity.Success);
    }

    private async Task BrowseForBeepAsync(SettingRowViewModel row)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".wav");
        picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_owner));
        var file = await picker.PickSingleFileAsync();
        if (file is null)
            return;

        row.TextValue = file.Path;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelCommand.Execute(null);
    }

    private void FocusFirstEditor()
    {
        var firstRow = ViewModel.SelectedCategoryRows.FirstOrDefault();
        if (firstRow is null)
            return;
        SettingsList.ScrollIntoView(firstRow);
        var container = SettingsList.ContainerFromItem(firstRow) as ListViewItem;
        if (container is null)
            return;
        var presenter = FindDescendant<FrameworkElement>(container, "EditorPresenter");
        Control? editor = presenter is null ? null : FindDescendant<Control>(presenter);
        (editor ?? (Control?)container)?.Focus(FocusState.Programmatic);
    }

    public void FocusDialog()
    {
        _ = _dispatcher.TryEnqueue(() =>
        {
            Focus(FocusState.Programmatic);
            FocusFirstEditor();
        });
    }

    private static T? FindDescendant<T>(DependencyObject root, string? name = null) where T : DependencyObject
    {
        if (root is null)
            return null;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                if (name is null)
                    return match;
                if (match is FrameworkElement fe && fe.Name == name)
                    return match;
            }
            var result = FindDescendant<T>(child, name);
            if (result is not null)
                return result;
        }
        return null;
    }
}

internal sealed class SettingsEditorTemplateSelector : DataTemplateSelector
{
    public DataTemplate? InfoTemplate { get; set; }
    public DataTemplate? NumberTemplate { get; set; }
    public DataTemplate? ToggleTemplate { get; set; }
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? HotkeyTemplate { get; set; }
    public DataTemplate? ComboTemplate { get; set; }
    public DataTemplate? FileTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is not SettingRowViewModel row)
            return base.SelectTemplateCore(item);

        return row.EditorType switch
        {
            SettingEditorType.Info => InfoTemplate,
            SettingEditorType.Number => NumberTemplate,
            SettingEditorType.Toggle => ToggleTemplate,
            SettingEditorType.Text => TextTemplate,
            SettingEditorType.Hotkey => HotkeyTemplate,
            SettingEditorType.Combo => ComboTemplate,
            SettingEditorType.FilePath => FileTemplate,
            _ => null
        };
    }
}

internal sealed class SettingsValidationBrushConverter : Microsoft.UI.Xaml.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is SettingValidationState state)
        {
            return state switch
            {
                SettingValidationState.Error => (Brush)Application.Current.Resources["TextFillColorCriticalBrush"],
                SettingValidationState.Warning => (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                _ => (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
        }
        return (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

internal sealed class StringToVisibilityConverter : Microsoft.UI.Xaml.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

internal sealed class BoolToVisibilityConverter : Microsoft.UI.Xaml.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool flag && flag)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

internal sealed class ErrorMessageVisibilityConverter : Microsoft.UI.Xaml.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is SettingValidationState state && state == SettingValidationState.Error
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
