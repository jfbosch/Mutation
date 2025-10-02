using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;

namespace Mutation.Ui;

public sealed partial class MainWindow
{
    private ContentDialog? _settingsDialog; // cached settings dialog moved to this partial

    // Hamburger / Menu related handlers split into partial for clarity
    private void Hamburger_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
            FlyoutBase.ShowAttachedFlyout(fe);
    }

    private void Hamburger_AccessKeyInvoked(object sender, AccessKeyInvokedEventArgs args)
    {
        args.Handled = true;
        if (sender is FrameworkElement fe)
            FlyoutBase.ShowAttachedFlyout(fe);
    }

    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsDialog == null)
        {
            _settingsDialog = BuildSettingsDialog();
        }
        _settingsDialog.XamlRoot = (this.Content as FrameworkElement)?.XamlRoot; // ensure root
        _ = _settingsDialog.ShowAsync();
    }

    private ContentDialog BuildSettingsDialog()
    {
        // Simple placeholder settings dialog (extend later with real settings controls)
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "Settings", FontSize = 20 });
        panel.Children.Add(new TextBlock { Text = "Add settings controls here.", TextWrapping = TextWrapping.Wrap });

        var dialog = new ContentDialog
        {
            Title = "Settings",
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = panel
        };
        AutomationProperties.SetName(dialog, "Settings dialog");
        AutomationProperties.SetHelpText(dialog, "Application settings");
        dialog.PrimaryButtonClick += (_, __) => { /* Persist settings if controls added */ ShowStatus("Settings", "Settings saved.", InfoBarSeverity.Success); };
        return dialog;
    }

}
