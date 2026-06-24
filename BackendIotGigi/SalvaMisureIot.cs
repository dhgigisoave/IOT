using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BackendIotGigi.Models;

namespace BackendIotGigi
{
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
				_logger.LogInformation("version 4 - producing MeasurementDocument");
				string rawMessage = messages[0];

				string deviceId = "UnknownDevice";
				var results = new List<object>();

				using (JsonDocument doc = JsonDocument.Parse(rawMessage))
				{
					var root = doc.RootElement;

					if (root.TryGetProperty("deviceId", out var deviceIdProp))
					{
						deviceId = deviceIdProp.GetString() ?? deviceId;
					}

					// temperature sensors
					if (root.TryGetProperty("temperatureSensor", out var tempSensors) && tempSensors.ValueKind == JsonValueKind.Array)
					{
						foreach (var sensor in tempSensors.EnumerateArray())
						{
							string sensorId = sensor.TryGetProperty("sensorId", out var sId) ? sId.ToString() : Guid.NewGuid().ToString();
							string sensorType = "temperature";

							if (sensor.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
							{
								foreach (var dataItem in dataArr.EnumerateArray())
								{
									// parse date/time
									DateTimeOffset timestamp;
									try
									{
										string date = dataItem.TryGetProperty("date", out var d) ? d.GetString() ?? "" : "";
										string time = dataItem.TryGetProperty("time", out var t) ? t.GetString() ?? "" : "00:00";
										timestamp = DateTimeOffset.ParseExact($"{date} {time}", new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
									}
									catch
									{
										timestamp = DateTimeOffset.UtcNow;
									}

									// parse temperature value (strip unit)
									double readingValue = 0.0;
									string readingUnit = "C";
									if (dataItem.TryGetProperty("temperature", out var tempProp) && tempProp.ValueKind == JsonValueKind.String)
									{
										string tempStr = tempProp.GetString() ?? "";
										tempStr = tempStr.Replace("°", "").Replace("C", "", StringComparison.InvariantCultureIgnoreCase).Trim();
										tempStr = tempStr.Replace(",", ".", StringComparison.InvariantCulture); // handle decimal comma
																												// remove any thousands separator left (e.g., "3,200.00")
										tempStr = tempStr.Replace(" ", "").Replace(" ", "");
										if (!double.TryParse(tempStr, NumberStyles.Any, CultureInfo.InvariantCulture, out readingValue))
										{
											readingValue = 0;
										}
									}

									var measurement = new MeasurementDocument(
										id: Guid.NewGuid().ToString(),
										sensorId: sensorId,
										deviceId: deviceId,
										sensorType: sensorType,
										timestamp: timestamp,
										readingValue: readingValue,
										readingUnit: readingUnit,
										coordinates: null,
										raw: dataItem.Clone(),
										ingestTimestamp: DateTimeOffset.UtcNow
									);

									results.Add(measurement);
								}
							}
						}
					}

					// speed sensors
					if (root.TryGetProperty("speedSensor", out var speedSensors) && speedSensors.ValueKind == JsonValueKind.Array)
					{
						foreach (var sensor in speedSensors.EnumerateArray())
						{
							string sensorId = sensor.TryGetProperty("sensorId", out var sId) ? sId.ToString() : Guid.NewGuid().ToString();
							string sensorType = "speed";

							double readingValue = 0.0;
							string readingUnit = "m/s";
							MeasurementDocument? measurement = null;

							// coordinates
							Coordinates? coords = null;
							if (sensor.TryGetProperty("coordinates", out var coordsProp) && coordsProp.ValueKind == JsonValueKind.Object)
							{
								double x = coordsProp.TryGetProperty("x", out var xp) && xp.TryGetDouble(out var xd) ? xd : 0.0;
								double y = coordsProp.TryGetProperty("y", out var yp) && yp.TryGetDouble(out var yd) ? yd : 0.0;
								coords = new Coordinates(x, y);
							}

							if (sensor.TryGetProperty("speed", out var speedProp) && speedProp.ValueKind == JsonValueKind.String)
							{
								string speedStr = speedProp.GetString() ?? "";
								// remove unit and spaces
								speedStr = speedStr.Replace("m/s", "", StringComparison.InvariantCultureIgnoreCase).Trim();
								// remove thousands separators (commas) and normalize decimal comma
								speedStr = speedStr.Replace(",", "", StringComparison.InvariantCulture); // remove thousands comma
								speedStr = speedStr.Replace(" ", "", StringComparison.InvariantCulture);
								speedStr = speedStr.Replace(" ", "");
								speedStr = speedStr.Replace(",", ".", StringComparison.InvariantCulture);
								if (!double.TryParse(speedStr, NumberStyles.Any, CultureInfo.InvariantCulture, out readingValue))
								{
									readingValue = 0.0;
								}
							}

							measurement = new MeasurementDocument(
								id: Guid.NewGuid().ToString(),
								sensorId: sensorId,
								deviceId: deviceId,
								sensorType: sensorType,
								timestamp: DateTimeOffset.UtcNow, // no timestamp in sample -> use ingest time
								readingValue: readingValue,
								readingUnit: readingUnit,
								coordinates: coords,
								raw: sensor.Clone(),
								ingestTimestamp: DateTimeOffset.UtcNow
							);

							results.Add(measurement);
						}
					}
				}

				_logger.LogInformation($"Prepared {results.Count} measurement(s) for Cosmos DB.");
				if (results.Count == 0) return null;
				return results.Count == 1 ? results[0] : (object)results;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Errore durante l'elaborazione dei messaggi IoT.");
				return null;
			}
		}
	}
}