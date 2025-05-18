using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using CognitiveSupport;
using Mutation.Ui.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Mutation.Ui
{
	/// <summary>
	/// An empty window that can be used on its own or navigated to within a Frame.
	/// </summary>
        public sealed partial class MainWindow : Window
        {
                private readonly ClipboardManager _clipboard;
                private readonly UiStateManager _uiStateManager;
                private readonly AudioDeviceManager _audioDeviceManager;
                private readonly OcrManager _ocrManager;

                public MainWindow(ClipboardManager clipboard, UiStateManager uiStateManager, AudioDeviceManager audioDeviceManager, OcrManager ocrManager)
                {
                        _clipboard = clipboard;
                        _uiStateManager = uiStateManager;
                        _audioDeviceManager = audioDeviceManager;
                        _ocrManager = ocrManager;
                        InitializeComponent();
                        this.Closed += MainWindow_Closed;
                }

                private void MainWindow_Closed(object sender, WindowEventArgs args)
                {
                        _uiStateManager.Save(this);
                }

                private void BtnBeep_Click(object sender, RoutedEventArgs e)
                {
                        BeepPlayer.Play(BeepType.Success);
                }

                private void CopyText_Click(object sender, RoutedEventArgs e)
                {
                        _clipboard.SetText(TxtClipboard.Text);
                }

                private void BtnToggleMic_Click(object sender, RoutedEventArgs e)
                {
                        _audioDeviceManager.ToggleMute();
                }

                private async void BtnScreenshot_Click(object sender, RoutedEventArgs e)
                {
                        await _ocrManager.TakeScreenshotToClipboardAsync();
                }

                private async void BtnScreenshotOcr_Click(object sender, RoutedEventArgs e)
                {
                        var result = await _ocrManager.TakeScreenshotAndExtractTextAsync(OcrReadingOrder.TopToBottomColumnAware);
                        TxtClipboard.Text = result.Message;
                }
        }
}
