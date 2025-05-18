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

                public MainWindow(ClipboardManager clipboard, UiStateManager uiStateManager)
                {
                        _clipboard = clipboard;
                        _uiStateManager = uiStateManager;
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
        }
}
