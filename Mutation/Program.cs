using AudioSwitcher.AudioApi.CoreAudio;
using CognitiveSupport;
using Deepgram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using OpenAI.Managers;

namespace Mutation;

internal static class Program
{
	const string OpenAiHttpClientName = "openai-http-client";

	[STAThread]
	static void Main()
	{
		// To customize application configuration such as set high DPI serviceSettings or default font,
		// see https://aka.ms/applicationconfiguration.

		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		ApplicationConfiguration.Initialize();

		try
		{
			HostApplicationBuilder builder = CreateApplicationBuilder();
			using IHost host = builder.Build();

			using var serviceScope = host.Services.CreateScope();
			var services = serviceScope.ServiceProvider;
			var mainForm = services.GetRequiredService<MutationForm>();

			Application.Run(mainForm);
		}
		catch (Exception ex)
		{
			MessageBox.Show($"Error starting up: {ex.ToString()}");
		}
	}

	private static HostApplicationBuilder CreateApplicationBuilder()
	{
		HostApplicationBuilder builder = Host.CreateApplicationBuilder();

		var settingsManager = CreateSettingsManager();
		var settings = settingsManager.LoadAndEnsureSettings();
		BeepPlayer.Initialize ( settings );

		if ( BeepPlayer.LastInitializationIssues.Count > 0 )
		{
			string message = "The following issues were found with the custom beep settings:\n\n" +
					 string.Join("\n", BeepPlayer.LastInitializationIssues);

			System.Windows.Forms.MessageBox.Show (
				message,
				"Custom Beep Settings Issues",
				System.Windows.Forms.MessageBoxButtons.OK,
				System.Windows.Forms.MessageBoxIcon.Warning
			);
		}

		builder.Services.AddSingleton<ISettingsManager>(settingsManager);
		builder.Services.AddSingleton<CognitiveSupport.Settings>(settings);

                builder.Services.AddSingleton<CoreAudioController>();
                builder.Services.AddSingleton<AudioDeviceManager>();
                builder.Services.AddSingleton<ClipboardManager>();

		builder.Services.AddSingleton<IOcrService>(
			new OcrService(
				settings.AzureComputerVisionSettings?.ApiKey ?? string.Empty,
				settings.AzureComputerVisionSettings?.Endpoint ?? string.Empty,
				settings.AzureComputerVisionSettings?.TimeoutSeconds ?? 30));

                builder.Services.AddSingleton<OcrManager>();
		builder.Services.AddSingleton<ILlmService>(
			new LlmService(
				settings.LlmSettings?.ApiKey ?? string.Empty,
				settings.LlmSettings?.ResourceName ?? string.Empty,
				settings.LlmSettings?.ModelDeploymentIdMaps ?? new List<LlmSettings.ModelDeploymentIdMap>()));

		builder.Services.AddHttpClient(OpenAiHttpClientName);

                AddSpeechToTextServices(builder, settings);

                builder.Services.AddSingleton<ITextToSpeechService, TextToSpeechService>();

                builder.Services.AddSingleton<HotkeyManager>();
                builder.Services.AddSingleton<UiStateManager>();

                builder.Services.AddSingleton<TranscriptFormatter>();
                builder.Services.AddSingleton<TranscriptReviewer>();
                builder.Services.AddSingleton<SoundFeedbackManager>();

                builder.Services.AddSingleton<MutationForm>();

		return builder;
	}

	private static void AddSpeechToTextServices(
		HostApplicationBuilder builder,
		Settings settings)
	{
		builder.Services.AddSingleton<ISpeechToTextService[]>(x =>
		{
			List<ISpeechToTextService> speechToTextServices = new();
			var sttSettings = settings.SpeetchToTextSettings?.Services ?? Array.Empty<SpeetchToTextServiceSettings>();
			foreach (SpeetchToTextServiceSettings serviceSettings in sttSettings)
			{
				switch (serviceSettings.Provider)
				{
					case SpeechToTextProviders.OpenAi:
						speechToTextServices.Add(CreateWhisperSpeechToTextService(builder, serviceSettings, x));
						break;
					case SpeechToTextProviders.Deepgram:
						speechToTextServices.Add(CreateDeepgramSpeechToTextService(builder, serviceSettings));
						break;
					default:
						throw new NotSupportedException($"The SpeetchToText service '{serviceSettings.Provider}' is not supported.");
				}
			}
			return speechToTextServices.ToArray();
		});
	}

	private static ISpeechToTextService CreateWhisperSpeechToTextService(
		HostApplicationBuilder builder,
		SpeetchToTextServiceSettings serviceSettings,
		IServiceProvider diServiceProvider)
	{
		string baseDomain = serviceSettings.BaseDomain?.Trim() ?? string.Empty;

		OpenAiOptions options = new OpenAiOptions
		{
			ApiKey = serviceSettings.ApiKey ?? string.Empty,
			BaseDomain = baseDomain,
		};

		IHttpClientFactory httpClientFactory = diServiceProvider.GetRequiredService<IHttpClientFactory>();
		HttpClient httpClient = httpClientFactory.CreateClient(OpenAiHttpClientName);
		var openAIService = new OpenAIService(options, httpClient);

		return new OpenAiSpeechToTextService(
			serviceSettings.Name ?? string.Empty,
			openAIService,
			serviceSettings.ModelId ?? string.Empty,
			serviceSettings.TimeoutSeconds > 0 ? serviceSettings.TimeoutSeconds : 10);
	}

	private static ISpeechToTextService CreateDeepgramSpeechToTextService(
		HostApplicationBuilder builder,
		SpeetchToTextServiceSettings serviceSettings)
	{
		Deepgram.Clients.Interfaces.v1.IListenRESTClient deepgramClient = ClientFactory.CreateListenRESTClient(
			serviceSettings.ApiKey ?? string.Empty);

		return new DeepgramSpeechToTextService(
			serviceSettings.Name ?? string.Empty,
			deepgramClient,
			serviceSettings.ModelId ?? string.Empty,
			serviceSettings.TimeoutSeconds > 0 ? serviceSettings.TimeoutSeconds : 10);
	}

	private static SettingsManager CreateSettingsManager()
	{
		try
		{
			string filePath = "Mutation.json";
			SettingsManager settingsManager = new SettingsManager(filePath);
			return settingsManager;
		}
		catch (Exception ex)
			when (ex.Message.ToLower().Contains("could not find the serviceSettings"))
		{
			//MessageBox.Show(this, $"Failed to load serviceSettings: {ex.Message}", "Unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			throw;
		}
	}

        // legacy method retained for potential console scenarios
        private static void BeepFail(int numberOfBeeps = 1)
        {
                var manager = new SoundFeedbackManager();
                manager.BeepFailure(numberOfBeeps);
        }

}