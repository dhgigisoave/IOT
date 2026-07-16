using BackendIotGigi.Models;
using BackendIotGigi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace BackendIotGigi
{
	public class LeggiMisure
	{
		private readonly ILogger<LeggiMisure> _logger;

		public LeggiMisure(ILogger<LeggiMisure> logger)
		{
			_logger = logger;
		}

		[Function("LeggiMisure")]
		public async Task<HttpResponseData> Run(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "misure")] HttpRequestData req)
		{
			var client = new CosmosClient(Environment.GetEnvironmentVariable("CosmosDBConnectionString"));
			var service = new SnapshotService(client, "IoTDatabase", "HD35Container");

			// valori di default
			var start = DateTime.UtcNow.AddHours(-1);
			var end = DateTime.UtcNow;

			try
			{
				var query = QueryHelpers.ParseQuery(req.Url.Query);

				if (query.TryGetValue("start", out var startVals) && !string.IsNullOrWhiteSpace(startVals))
				{
					if (!TryParseDateOrEpoch(startVals.ToString(), out var parsedStart))
					{
						_logger.LogWarning("Parametro 'start' non valido: {value}", startVals.ToString());
					}
					else
					{
						start = parsedStart;
					}
				}

				if (query.TryGetValue("end", out var endVals) && !string.IsNullOrWhiteSpace(endVals))
				{
					if (!TryParseDateOrEpoch(endVals.ToString(), out var parsedEnd))
					{
						_logger.LogWarning("Parametro 'end' non valido: {value}", endVals.ToString());
					}
					else
					{
						end = parsedEnd;
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Errore parsing querystring; uso valori di default per start/end.");
			}

			_logger.LogInformation("Lettura misure richiesta dal frontend: start={start}, end={end}", start, end);
			var misure = await service.GetLatestEnrichedSnapshotsAsync(start, end);
			var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
			await response.WriteAsJsonAsync(misure);
			return response;
		}

		private static bool TryParseDateOrEpoch(string input, out DateTime utc)
		{
			utc = default;
			// Try ISO / standard date formats
			if (DateTime.TryParse(input, CultureInfo.InvariantCulture
				, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
			{
				utc = dt.ToUniversalTime();
				return true;
			}

			// Try epoch (seconds or milliseconds)
			if (long.TryParse(input, out var epoch))
			{
				try
				{
					if (input.Length > 10) // presumibilmente milliseconds
						utc = DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime;
					else
						utc = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
					return true;
				}
				catch { /* cadere fuori */ }
			}

			return false;
		}
	}
}