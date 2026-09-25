using BackendIotGigi.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Configuration;

namespace BackendIotGigi;

public class TimerTriggers
{
    private readonly ILogger _logger;

    public TimerTriggers(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TimerTriggers>();
    }

    [Function("TimerTriggers")]
	public void Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer,
		[CosmosDBInput(
				databaseName: "senseca-pd",
				containerName: "deviceReadings",
				Connection = "CosmosDBConnection")]HD35Payload toDoItem)
    {

		_logger.LogInformation("C# Timer trigger function executed at: {executionTime}", DateTime.Now);
        
        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }
    }
}