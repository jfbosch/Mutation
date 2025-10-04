using CognitiveSupport;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Mutation.Ui.Views;
using Mutation.Ui.Views.Settings;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics;

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
            _settingsDialog = new SettingsWindow(
                this,
                _settings,
                _settingsManager,
                _audioDeviceManager,
                ApplySettingsDialogResult);
        }

        if (Content is FrameworkElement fe)
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

    private void ApplySettingsDialogResult(SettingsDialogSaveResult result)
    {
        if (result.ApplyMultiLinePreferences)
        {
            ApplyMultiLineTextBoxPreferences();
        }

        if (result.RefreshHotkeyVisuals)
        {
            InitializeHotkeyVisuals();
        }

        if (result.RefreshMicrophoneSelection)
        {
            RefreshMicrophoneSelection(result.ActiveMicrophoneName);
        }

        if (result.ReloadBeeps)
        {
            BeepPlayer.Initialize(_settings);
        }

        if (result.ResetWindowPosition)
        {
            CenterWindowOnCurrentDisplay();
        }

        ShowStatus("Settings", "Settings saved.", InfoBarSeverity.Success);
    }

    private void RefreshMicrophoneSelection(string? preferredDevice)
    {
        var devices = _audioDeviceManager.CaptureDevices.ToList();
        CmbMicrophone.ItemsSource = devices;

        if (!string.IsNullOrWhiteSpace(preferredDevice))
        {
            var match = devices.FirstOrDefault(d => GetDeviceFriendlyName(d) == preferredDevice);
            if (match is not null)
            {
                CmbMicrophone.SelectedItem = match;
            }
            else
            {
                RestorePersistedMicrophoneSelection(devices);
            }
        }
        else
        {
            RestorePersistedMicrophoneSelection(devices);
        }

        if (CmbMicrophone.SelectedItem is CoreAudio.MMDevice device)
        {
            _audioDeviceManager.SelectMicrophone(device);
            RestartMicrophoneVisualizationCapture();
        }
    }

    private void CenterWindowOnCurrentDisplay()
    {
        var appWindow = AppWindow;
        if (appWindow is null)
        {
            return;
        }

        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var bounds = displayArea.WorkArea;
        int width = appWindow.Size.Width;
        int height = appWindow.Size.Height;
        int x = bounds.X + Math.Max(0, (bounds.Width - width) / 2);
        int y = bounds.Y + Math.Max(0, (bounds.Height - height) / 2);
        appWindow.Move(new PointInt32(x, y));
    }
}
