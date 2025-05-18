using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Mutation.Ui
{
	/// <summary>
	/// An empty window that can be used on its own or navigated to within a Frame.
	/// </summary>
        public sealed partial class MainWindow : Window
        {
                private readonly Services.OcrManager _ocrManager;
                private readonly Services.HotkeyManager _hotkeyManager;
                private readonly Services.UiStateManager _uiStateManager;

                public MainWindow(Services.OcrManager ocrManager,
                                   Services.HotkeyManager hotkeyManager,
                                   Services.UiStateManager uiStateManager)
                {
                        _ocrManager = ocrManager;
                        _hotkeyManager = hotkeyManager;
                        _uiStateManager = uiStateManager;

                        InitializeComponent();
                        Loaded += OnLoaded;
                        Closing += OnClosing;
                }

                private void OnLoaded(object sender, RoutedEventArgs e)
                {
                        _uiStateManager.Restore(this);
                        _hotkeyManager.Initialize(this);
                        btnOcr.Click += async (_, _) =>
                        {
                                var result = await _ocrManager.TakeScreenshotAndExtractText(CognitiveSupport.OcrReadingOrder.TopToBottomColumnAware);
                                if (result.Success)
                                        txtOcr.Text = result.Message;
                        };
                }

                private void OnClosing(object sender, WindowEventArgs e)
                {
                        _uiStateManager.Save(this);
                        _hotkeyManager.UnregisterAll();
                }
        }
}
