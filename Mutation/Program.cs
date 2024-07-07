using AudioSwitcher.AudioApi.CoreAudio;
using CognitiveSupport;
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

		builder.Services.AddSingleton<HttpClient>(x =>
		{
			HttpClient httpClient = new HttpClient();
			httpClient.Timeout = TimeSpan.FromSeconds(30);
			return httpClient;
		});

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

			HttpClient httpClient = x.GetRequiredService<HttpClient>();
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
			//var settings = settingsManager.LoadAndEnsureSettings();
		}
		catch (Exception ex)
			when (ex.Message.ToLower().Contains("could not find the settings"))
		{
			//MessageBox.Show(this, $"Failed to load settings: {ex.Message}", "Unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			throw;
		}
	}


}