using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using AudioSwitcher.AudioApi.CoreAudio;
using Deepgram;
using System.Net.Http;
using OpenAI;
using OpenAI.Managers;
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
        private const string OpenAiHttpClientName = "openai-http-client";

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
                builder.Services.AddSingleton<CoreAudioController>();
                builder.Services.AddSingleton<AudioDeviceManager>();
                builder.Services.AddSingleton<OcrManager>(sp =>
                        new OcrManager(settings,
                                       sp.GetRequiredService<IOcrService>(),
                                       sp.GetRequiredService<ClipboardManager>()));
                builder.Services.AddSingleton<HotkeyManager>(sp =>
                        new HotkeyManager(sp.GetRequiredService<MainWindow>(), sp.GetRequiredService<Settings>()));
                builder.Services.AddSingleton<ILlmService>(
                        new LlmService(
                                settings.LlmSettings?.ApiKey ?? string.Empty,
                                settings.LlmSettings?.ResourceName ?? string.Empty,
                                settings.LlmSettings?.ModelDeploymentIdMaps ?? new List<LlmSettings.ModelDeploymentIdMap>()));
                builder.Services.AddSingleton<TranscriptFormatter>();
                builder.Services.AddSingleton<TranscriptReviewer>();
                builder.Services.AddSingleton<ITextToSpeechService, TextToSpeechService>();
                builder.Services.AddHttpClient(OpenAiHttpClientName);
                AddSpeechToTextServices(builder, settings);
                builder.Services.AddSingleton<MainWindow>();

                _host = builder.Build();

                _window = _host.Services.GetRequiredService<MainWindow>();
                var ui = _host.Services.GetRequiredService<UiStateManager>();
                ui.Restore(_window);

                _window.Activate();

                if (BeepPlayer.LastInitializationIssues.Count > 0)
                {
                        var dialog = new ContentDialog
                        {
                                Title = "Custom Beep Settings Issues",
                                Content = "The following issues were found with the custom beep settings:\n\n" + string.Join("\n", BeepPlayer.LastInitializationIssues),
                                CloseButtonText = "OK",
                                XamlRoot = _window.Content.XamlRoot
                        };
                        await dialog.ShowAsync();
                }

                var ocrMgr = _host.Services.GetRequiredService<OcrManager>();
                ocrMgr.InitializeWindow(_window);

                // Register global hotkeys
                var hkManager = _host.Services.GetRequiredService<HotkeyManager>();
                var settingsSvc = _host.Services.GetRequiredService<Settings>();

                if (!string.IsNullOrWhiteSpace(settingsSvc.AzureComputerVisionSettings?.ScreenshotHotKey))
                {
                        hkManager.RegisterHotkey(
                                Hotkey.Parse(settingsSvc.AzureComputerVisionSettings.ScreenshotHotKey!),
                                () => _ = ocrMgr.TakeScreenshotToClipboardAsync());
                }
                if (!string.IsNullOrWhiteSpace(settingsSvc.AzureComputerVisionSettings?.ScreenshotOcrHotKey))
                {
                        hkManager.RegisterHotkey(
                                Hotkey.Parse(settingsSvc.AzureComputerVisionSettings.ScreenshotOcrHotKey!),
                                () => _ = ocrMgr.TakeScreenshotAndExtractTextAsync(OcrReadingOrder.TopToBottomColumnAware));
                }

                if (!string.IsNullOrWhiteSpace(settingsSvc.AzureComputerVisionSettings?.OcrHotKey))
                {
                        hkManager.RegisterHotkey(
                                Hotkey.Parse(settingsSvc.AzureComputerVisionSettings.OcrHotKey!),
                                () => _ = ocrMgr.ExtractTextFromClipboardImageAsync(OcrReadingOrder.TopToBottomColumnAware));
                }

                if (!string.IsNullOrWhiteSpace(settingsSvc.AudioSettings?.MicrophoneToggleMuteHotKey))
                {
                        hkManager.RegisterHotkey(
                                Hotkey.Parse(settingsSvc.AudioSettings.MicrophoneToggleMuteHotKey!),
                                () => _window.DispatcherQueue.TryEnqueue(() => ((MainWindow)_window).BtnToggleMic_Click(null!, null!)));
                }

                if (!string.IsNullOrWhiteSpace(settingsSvc.SpeetchToTextSettings?.SpeechToTextHotKey))
                {
                        hkManager.RegisterHotkey(
                                Hotkey.Parse(settingsSvc.SpeetchToTextSettings.SpeechToTextHotKey!),
                                () => _window.DispatcherQueue.TryEnqueue(async () => await ((MainWindow)_window).StartStopSpeechToTextAsync()));
                }

                if (!string.IsNullOrWhiteSpace(settingsSvc.TextToSpeechSettings?.TextToSpeechHotKey))
                {
                        hkManager.RegisterHotkey(
                                Hotkey.Parse(settingsSvc.TextToSpeechSettings.TextToSpeechHotKey!),
                                () => _window.DispatcherQueue.TryEnqueue(() => ((MainWindow)_window).BtnTextToSpeech_Click(null!, null!)));
                }

                hkManager.RegisterRouterHotkeys();
        }

        private static void AddSpeechToTextServices(HostApplicationBuilder builder, Settings settings)
        {
                builder.Services.AddSingleton<ISpeechToTextService[]>(sp =>
                {
                        List<ISpeechToTextService> services = new();
                        var sttSettings = settings.SpeetchToTextSettings?.Services ?? Array.Empty<SpeetchToTextServiceSettings>();
                        foreach (var serviceSettings in sttSettings)
                        {
                                switch (serviceSettings.Provider)
                                {
                                        case SpeechToTextProviders.OpenAi:
                                                services.Add(CreateWhisperSpeechToTextService(builder, serviceSettings, sp));
                                                break;
                                        case SpeechToTextProviders.Deepgram:
                                                services.Add(CreateDeepgramSpeechToTextService(builder, serviceSettings));
                                                break;
                                        default:
                                                throw new NotSupportedException($"The SpeetchToText service '{serviceSettings.Provider}' is not supported.");
                                }
                        }
                        return services.ToArray();
                });
        }

        private static ISpeechToTextService CreateWhisperSpeechToTextService(HostApplicationBuilder builder, SpeetchToTextServiceSettings serviceSettings, IServiceProvider sp)
        {
                string baseDomain = serviceSettings.BaseDomain?.Trim() ?? string.Empty;

                OpenAiOptions options = new OpenAiOptions
                {
                        ApiKey = serviceSettings.ApiKey ?? string.Empty,
                        BaseDomain = baseDomain,
                };

                IHttpClientFactory httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                HttpClient httpClient = httpClientFactory.CreateClient("openai-http-client");
                var openAIService = new OpenAIService(options, httpClient);

                return new OpenAiSpeechToTextService(
                        serviceSettings.Name ?? string.Empty,
                        openAIService,
                        serviceSettings.ModelId ?? string.Empty,
                        serviceSettings.TimeoutSeconds > 0 ? serviceSettings.TimeoutSeconds : 10);
        }

        private static ISpeechToTextService CreateDeepgramSpeechToTextService(HostApplicationBuilder builder, SpeetchToTextServiceSettings serviceSettings)
        {
                Deepgram.Clients.Interfaces.v1.IListenRESTClient deepgramClient = ClientFactory.CreateListenRESTClient(serviceSettings.ApiKey ?? string.Empty);

                return new DeepgramSpeechToTextService(
                        serviceSettings.Name ?? string.Empty,
                        deepgramClient,
                        serviceSettings.ModelId ?? string.Empty,
                        serviceSettings.TimeoutSeconds > 0 ? serviceSettings.TimeoutSeconds : 10);
        }
	}
}
