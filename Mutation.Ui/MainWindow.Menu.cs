using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using Mutation.Ui.Views;
using System.Threading.Tasks;

namespace Mutation.Ui;

public sealed partial class MainWindow
{
    private SettingsWindow? _settingsDialog; // cached settings dialog moved to this partial

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

    private async void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        await ShowSettingsDialogAsync();
    }

    private async Task ShowSettingsDialogAsync()
    {
        if (_settingsDialog is null)
        {
            _settingsDialog = new SettingsWindow(this, _settings, _settingsManager, _audioDeviceManager);
        }

        if (this.Content is FrameworkElement fe)
        {
            _settingsDialog.XamlRoot = fe.XamlRoot;
        }

        if (_settingsDialog.IsShowing)
        {
            _settingsDialog.FocusDialog();
            return;
        }

        await _settingsDialog.ShowAsync();
    }

    private async void HeaderSettings_Click(object sender, RoutedEventArgs e)
    {
        await ShowSettingsDialogAsync();
    }

    private void HamburgerAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (BtnHamburger is FrameworkElement button)
        {
            FlyoutBase.ShowAttachedFlyout(button);
        }
    }

}
