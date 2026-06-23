using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration; // <-- Controlla che ci sia questo
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
	.ConfigureFunctionsWebApplication() // o .ConfigureFunctionsWorkerDefaults() se non usi HTTP
	.ConfigureAppConfiguration(config =>
	{
		// Forza l'applicazione a leggere il file local.settings.json in locale
		config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
		config.AddEnvironmentVariables();
	})
	.Build();

host.Run();