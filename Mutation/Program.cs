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

		builder.Services.AddSingleton<ISettingsManager>(settingsManager);
		builder.Services.AddSingleton<CognitiveSupport.Settings>(settings);

		builder.Services.AddSingleton<CoreAudioController>();

		builder.Services.AddSingleton<IOcrService>(
			new OcrService(settings.AzureComputerVisionSettings.ApiKey, settings.AzureComputerVisionSettings.Endpoint));

		builder.Services.AddSingleton<ILlmService>(
			new LlmService(
				settings.LlmSettings.ApiKey,
				settings.LlmSettings.ResourceName,
				settings.LlmSettings.ModelDeploymentIdMaps));


		builder.Services.AddHttpClient(OpenAiHttpClientName);

		AddSpeechToTextServices(builder, settings);

		builder.Services.AddSingleton<ITextToSpeechService, TextToSpeechService>();

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
			foreach (SpeetchToTextServiceSettings serviceSettings in settings.SpeetchToTextSettings.Services)
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
		string baseDomain = serviceSettings.BaseDomain?.Trim();
		if (baseDomain == "")
			baseDomain = null;

		OpenAiOptions options = new OpenAiOptions
		{
			ApiKey = serviceSettings.ApiKey,
			BaseDomain = baseDomain,
		};

		IHttpClientFactory httpClientFactory = diServiceProvider.GetRequiredService<IHttpClientFactory>();
		HttpClient httpClient = httpClientFactory.CreateClient(OpenAiHttpClientName);
		var openAIService = new OpenAIService(options, httpClient);

		return new WhisperSpeechToTextService(
			serviceSettings.Name,
			openAIService,
			serviceSettings.ModelId);
	}

	private static ISpeechToTextService CreateDeepgramSpeechToTextService(
		HostApplicationBuilder builder,
		SpeetchToTextServiceSettings serviceSettings)
	{
		Deepgram.Clients.Interfaces.v1.IListenRESTClient deepgramClient = ClientFactory.CreateListenRESTClient(
			serviceSettings.ApiKey);

		return new DeepgramSpeechToTextService(
			serviceSettings.Name,
			deepgramClient,
			serviceSettings.ModelId);
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

	private static void BeepFail(int numberOfBeeps = 1)
	{
		for (int i = 0; i < numberOfBeeps; i++)
			Console.Beep(400 + (100 * numberOfBeeps), 100);
	}

}