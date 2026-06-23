using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

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
		public IEnumerable<MisuraCosmos> Run(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "misure")] HttpRequestData req,
			[CosmosDBInput(
			databaseName: "IoTDatabase",
			containerName: "Misure",
			Connection = "CosmosDBConnectionString",
			SqlQuery = "SELECT TOP 100 * FROM c ORDER BY c.timestamp DESC")]
		IEnumerable<MisuraCosmos> misure)
		{
			_logger.LogInformation("Lettura misure richiesta dal frontend");
			return misure;
		}
	}
}
