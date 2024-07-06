using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ConsoleDI.Example;
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

			HostApplicationBuilder builder = Host.CreateApplicationBuilder();

			builder.Services.AddSingleton<MutationForm>();

			builder.Services.AddTransient<IExampleTransientService, ExampleTransientService>();
			builder.Services.AddScoped<IExampleScopedService, ExampleScopedService>();
			builder.Services.AddSingleton<IExampleSingletonService, ExampleSingletonService>();
			builder.Services.AddTransient<ServiceLifetimeReporter>();

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


	static void ExemplifyServiceLifetime(
		IServiceProvider hostProvider,
		string lifetime)
	{
		using IServiceScope serviceScope = hostProvider.CreateScope();
		IServiceProvider provider = serviceScope.ServiceProvider;
		ServiceLifetimeReporter logger = provider.GetRequiredService<ServiceLifetimeReporter>();
		logger.ReportServiceLifetimeDetails(
			 $"{lifetime}: Call 1 to provider.GetRequiredService<ServiceLifetimeReporter>()");

		Console.WriteLine("...");

		logger = provider.GetRequiredService<ServiceLifetimeReporter>();
		logger.ReportServiceLifetimeDetails(
			 $"{lifetime}: Call 2 to provider.GetRequiredService<ServiceLifetimeReporter>()");

		Console.WriteLine();
	}
}