using BackendIotGigi;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
	.ConfigureFunctionsWebApplication()
	.ConfigureAppConfiguration(config =>
	{
		config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
		config.AddEnvironmentVariables();
	})
	.ConfigureServices((context, services) =>
	{
		services.AddSingleton(_ =>
			RegistryManager.CreateFromConnectionString(
				context.Configuration.GetValue<string>("IoTHubConnectionString2")));
	})
	.Build();

// Warmup connessione AMQP — ignora eccezioni (device inesistente va bene)
try
{
	var rm = host.Services.GetRequiredService<RegistryManager>();
	await rm.GetDeviceAsync("__warmup__");
}
catch { /* atteso: connessione aperta comunque */ }

await host.RunAsync();