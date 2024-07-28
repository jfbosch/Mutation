using AudioSwitcher.AudioApi.CoreAudio;
using CognitiveSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using OpenAI.Interfaces;
using OpenAI.Managers;
using Polly;
using Polly.Timeout;
using System.Net;

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
		//TODO: Consider adding this back with the version of poly that came as a dependency to Deepgram.
		//.AddStandardResilienceHandler(options =>
		//{
		//	options.Retry.ShouldHandle = async (args) =>
		//		 args.Outcome switch
		//		 {
		//			 { Exception: TimeoutRejectedException } => true,
		//			 { Exception: HttpRequestException } => true,
		//			 { Result: { StatusCode: HttpStatusCode.InternalServerError } } => true,
		//			 _ => false
		//		 };
		//	options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
		//	options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
		//	options.Retry.MaxRetryAttempts = 4;
		//	options.Retry.BackoffType = DelayBackoffType.Exponential;
		//	options.Retry.Delay = TimeSpan.FromMilliseconds(50);

		//	options.Retry.OnRetry = async args =>
		//	{
		//		BeepFail(args.AttemptNumber);
		//	};

		//});


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


		builder.Services.AddSingleton<ISpeechToTextService>(x =>
		{
			var openAIService = x.GetRequiredService<IOpenAIService>();

			return new SpeechToTextService(
				openAIService,
				settings.SpeetchToTextSettings.ModelId);
		});

		builder.Services.AddSingleton<ITextToSpeechService, TextToSpeechService>();

		builder.Services.AddSingleton<MutationForm>();

		return builder;
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