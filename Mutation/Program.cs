using AudioSwitcher.AudioApi.CoreAudio;
using CognitiveSupport;
using Deepgram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using OpenAI.Interfaces;
using OpenAI.Managers;

namespace Mutation;

internal static class Program
{
	[STAThread]
	static void Main()
	{
		// To customize application configuration such as set high DPI settings or default font,
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


		const string OpenAiHttpClient = "openai-http-client";
		builder.Services.AddHttpClient(OpenAiHttpClient);

		builder.Services.AddSingleton<IOpenAIService>(x =>
		{
			string baseDomain = settings.SpeetchToTextSettings.BaseDomain?.Trim();
			if (baseDomain == "")
				baseDomain = null;

			OpenAiOptions options = new OpenAiOptions
			{
				ApiKey = settings.SpeetchToTextSettings.ApiKey,
				BaseDomain = baseDomain,
			};

			IHttpClientFactory httpClientFactory = x.GetRequiredService<IHttpClientFactory>();
			HttpClient httpClient = httpClientFactory.CreateClient(OpenAiHttpClient);
			return new OpenAIService(options, httpClient);
		});


		switch (settings.SpeetchToTextSettings.Service)
		{
			case SpeechToTextServices.OpenAiWhisper:
				AddWhisperSpeechToTextService(builder, settings);
				break;
			case SpeechToTextServices.Deepgram:
				AddDeepgramSpeechToTextService(builder, settings);
				break;
			default:
				throw new NotSupportedException($"The SpeetchToText service '{settings.SpeetchToTextSettings.Service}' is not supported.");
		}


		builder.Services.AddSingleton<ITextToSpeechService, TextToSpeechService>();

		builder.Services.AddSingleton<MutationForm>();

		return builder;
	}

	private static void AddWhisperSpeechToTextService(HostApplicationBuilder builder, Settings settings)
	{
		builder.Services.AddSingleton<ISpeechToTextService>(x =>
		{
			var openAIService = x.GetRequiredService<IOpenAIService>();

			return new WhisperSpeechToTextService(
				openAIService,
				settings.SpeetchToTextSettings.ModelId);
		});
	}

	private static void AddDeepgramSpeechToTextService(HostApplicationBuilder builder, Settings settings)
	{
		builder.Services.AddSingleton<ISpeechToTextService>(x =>
		{
			Deepgram.Clients.Interfaces.v1.IListenRESTClient deepgramClient = ClientFactory.CreateListenRESTClient(
				settings.SpeetchToTextSettings.ApiKey);

			return new DeepgramSpeechToTextService(
				deepgramClient,
				settings.SpeetchToTextSettings.ModelId);
		});
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
			when (ex.Message.ToLower().Contains("could not find the settings"))
		{
			//MessageBox.Show(this, $"Failed to load settings: {ex.Message}", "Unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			throw;
		}
	}

	private static void BeepFail(int numberOfBeeps = 1)
	{
		for (int i = 0; i < numberOfBeeps; i++)
			Console.Beep(400 + (100 * numberOfBeeps), 100);
	}

}