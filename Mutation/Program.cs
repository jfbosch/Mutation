using CognitiveSupport;
using ConsoleDI.Example;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mutation.ConsoleDI.Example;

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
		//builder.Configuration.add
		//builder.Services.Configure

		var settingsManager = CreateSettingsManager();
		var settings = settingsManager.LoadAndEnsureSettings();
		builder.Services.AddSingleton<ISettingsManager>(settingsManager);
		builder.Services.AddSingleton<CognitiveSupport.Settings>(settings);

		builder.Services.AddSingleton<IOcrService>(
			new OcrService(settings.AzureComputerVisionSettings.ApiKey, settings.AzureComputerVisionSettings.Endpoint));

		builder.Services.AddSingleton<MutationForm>();

		//BookMark??
		builder.Services.AddTransient<IExampleTransientService, ExampleTransientService>();
		builder.Services.AddScoped<IExampleScopedService, ExampleScopedService>();
		builder.Services.AddSingleton<IExampleSingletonService, ExampleSingletonService>();
		builder.Services.AddTransient<ServiceLifetimeReporter>();
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