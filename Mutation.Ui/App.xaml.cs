using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using System;
using CognitiveSupport;
using Mutation.Ui.Services;

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
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
                HostApplicationBuilder builder = Host.CreateApplicationBuilder();

                var settingsManager = new SettingsManager();
                var settings = await settingsManager.LoadAsync();
                BeepPlayer.Initialize(settings);

                builder.Services.AddSingleton<ISettingsManager>(settingsManager);
                builder.Services.AddSingleton(settings);
                builder.Services.AddSingleton<ClipboardManager>();
                builder.Services.AddSingleton<UiStateManager>();
                builder.Services.AddSingleton<MainWindow>();

                _host = builder.Build();

                _window = _host.Services.GetRequiredService<MainWindow>();
                var ui = _host.Services.GetRequiredService<UiStateManager>();
                ui.Restore(_window);

                _window.Activate();
        }
	}
}
