using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using CognitiveSupport;
using Mutation.Ui.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Mutation.Ui
{
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
        public partial class App : Application
        {
                private Window? _window;
                private IHost? _host;
                public static Window? MainWindow { get; private set; }

		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Invoked when the application is launched.
		/// </summary>
		/// <param name="args">Details about the launch request and process.</param>
                protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
                {
                        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

                        var settingsManager = new SettingsManager("Mutation.json");
                        var settings = settingsManager.LoadAndEnsureSettings();

                        builder.Services.AddSingleton<ISettingsManager>(settingsManager);
                        builder.Services.AddSingleton(settings);

                        builder.Services.AddSingleton<ClipboardManager>();
                        builder.Services.AddSingleton<IOcrService>(new OcrService(
                                settings.AzureComputerVisionSettings?.ApiKey ?? string.Empty,
                                settings.AzureComputerVisionSettings?.Endpoint ?? string.Empty,
                                settings.AzureComputerVisionSettings?.TimeoutSeconds ?? 30));
                        builder.Services.AddSingleton<OcrManager>();
                        builder.Services.AddSingleton<HotkeyManager>();
                        builder.Services.AddSingleton<UiStateManager>();

                        builder.Services.AddSingleton<MainWindow>();

                        _host = builder.Build();

                        _window = _host.Services.GetRequiredService<MainWindow>();
                        MainWindow = _window;
                        _window.Activate();
                }
        }
}
