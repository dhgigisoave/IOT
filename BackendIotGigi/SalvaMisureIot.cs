using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackendIotGigi
{
	public record MisuraCosmos(
	[property: JsonPropertyName("id")] string Id,
	[property: JsonPropertyName("deviceId")] string DeviceId,
	[property: JsonPropertyName("timestamp")] DateTime Timestamp,
	[property: JsonPropertyName("dati")] JsonElement Dati
);
	public class SalvaMisureIot
	{
		private readonly ILogger<SalvaMisureIot> _logger;

		public SalvaMisureIot(ILogger<SalvaMisureIot> logger)
		{
			_logger = logger;
		}

		[Function("SalvaMisureIot")]
		[CosmosDBOutput(databaseName: "IoTDatabase", containerName: "Misure", Connection = "CosmosDBConnectionString")]
		public object? Run(
			[EventHubTrigger("%IoTHubName%", Connection = "IoTHubConnectionString", ConsumerGroup = "%ConsumerGroup%")] 
			string[] messages,
			FunctionContext context)
		{
			try
			{
				_logger.LogInformation("version 4");
				string rawMessage = messages[0];

				string deviceId = "UnknownDevice";
				JsonElement datiClonati;

				// Usiamo l'using SOLO per estrarre e clonare i dati in sicurezza
				using (JsonDocument doc = JsonDocument.Parse(rawMessage))
				{
					var root = doc.RootElement;
					if (root.TryGetProperty("deviceId", out var deviceIdProp))
					{
						deviceId = deviceIdProp.GetString() ?? "UnknownDevice";
					}

					// .Clone() copia il JSON in una nuova area di memoria gestita e sicura
					datiClonati = root.Clone();
				}

				// Ora che siamo FUORI dall'using, creiamo l'oggetto per Cosmos DB
				var ret = new
				{
					id = Guid.NewGuid().ToString(),
					deviceId = deviceId,
					timestamp = DateTime.UtcNow,
					dati = datiClonati // Questa memoria è salva e non verrà distrutta
				};

				_logger.LogInformation($"Messaggio formattato per Cosmos DB: {JsonSerializer.Serialize(ret)}");

				return ret;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Errore durante l'elaborazione dei messaggi IoT.");
				return null;
			}
		}
	}
}