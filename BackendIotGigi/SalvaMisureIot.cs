using Azure.Messaging.EventHubs;
using BackendIotGigi.Models;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace BackendIotGigi
{
	public class SalvaMisureIot
	{
		private readonly ILogger<SalvaMisureIot> _logger;
		private readonly IConfiguration _config;

		public SalvaMisureIot(ILogger<SalvaMisureIot> logger, IConfiguration config)
		{
			_logger = logger;
			_config = config;
		}

		[Function("SalvaMisureIot")]
		[CosmosDBOutput(databaseName: "IoTDatabase", containerName: "HD35Container", Connection = "CosmosDBConnectionString")]
		public async Task<object?> Run(
			[EventHubTrigger("%IoTHubName%", Connection = "IoTHubConnectionString", ConsumerGroup = "%ConsumerGroup%")]
			EventData[] events,
			FunctionContext context)
		{
			var results = new List<object>();
			try
			{
				_logger.LogInformation("version 6 - producing MeasurementDocument");

				foreach (var eventData in events)
				{
					// body
					var body = Encoding.UTF8.GetString(eventData.EventBody.ToArray());
					_logger.LogInformation($"Received message: {body}");

					// deviceId inoltrato da IoT Hub come proprietà applicativa
					string? deviceId = null;
					if (eventData.Properties != null && eventData.Properties.TryGetValue("iothub-connection-device-id", out var devIdObj))
					{
						deviceId = devIdObj?.ToString();
					}
					if (deviceId is null
						&& eventData.SystemProperties != null && eventData.SystemProperties.TryGetValue("iothub-connection-device-id", out var devIdObj2))
					{

						deviceId = devIdObj2?.ToString();
					}

					// fallback: prova a leggere deviceId dal JSON se non presente nelle properties
					if (string.IsNullOrEmpty(deviceId))
					{
						try
						{
							using var jd = JsonDocument.Parse(body);
							if (jd.RootElement.TryGetProperty("deviceId", out var did) && did.ValueKind == JsonValueKind.String)
								deviceId = did.GetString();
						}
						catch { /* ignore parse error */ }
					}

					_logger.LogInformation($"DeviceId resolved: {deviceId ?? "<unknown>"}");

					// decidere come usare deviceId: passarlo quando costruisci il documento da salvare
					var jsonDoc = JsonDocument.Parse(body);
					jsonDoc.RootElement.TryGetProperty("fw_rel", out var fw_rel);
					// legge la stessa connection usata dall'attributo Connection
					var iotHubConn = _config.GetValue<string>("IoTHubConnectionString2")
								   ?? Environment.GetEnvironmentVariable("IoTHubConnectionString2");

					var rm = RegistryManager.CreateFromConnectionString(iotHubConn);
					if (rm is not null
						&& !string.IsNullOrEmpty(deviceId))
					{
						if (fw_rel.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(fw_rel.GetString()))
							await UpdateConfig(body, results, rm);
						else
							await AddData(body, results, rm, deviceId);
					}
				}

				if (results.Count == 0) return null;
				return results;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Errore durante l'elaborazione dei messaggi IoT.");
				return null;
			}
		}

		private async Task UpdateConfig(string message, List<object> results, RegistryManager rm)
		{
			var data = JsonSerializer.Deserialize<HD35ConfigPayload>(message);
			if (data is not null)
			{
				var sensorIds = await RegisterDevice.RegisterSensors(rm, data);
				foreach (var sensor in data.Params)
				{
					var sensorId = $"{sensor.SerialNumber}_{sensor.Id}";
					var uniqueId = sensorIds.FirstOrDefault(s => s.sensorId == sensorId).uniqueId;
					var metadata = new SensorMetadata(
						DisplayName: $"{sensor.Label}-{sensor.SubLabel}",
						Unit: sensor.Unit,
						Id: uniqueId,
						SerialNumber: sensor.SerialNumber,
						IdChannel: sensor.Id,
						Qual: sensor.Qual,
						Format: sensor.Format,
						Offset: sensor.Offset,
						Scale: sensor.Scale,
						Timestamp: DateTime.Now
					);
					results.Add(metadata);
				}
			}
		}

		private async Task AddData(string message, List<object> results, RegistryManager rm, string deviceId)
		{
			var data = JsonSerializer.Deserialize<HD35Payload>(message);
			if (data is not null)
			{
				var twin = await rm.GetTwinAsync(deviceId);
				if (twin is not null)
				{
					foreach (var record in data.Data)
					{
						var measurement = new MeasurementSnapshot
						{
							Id = Guid.NewGuid().ToString(),
							DeviceId = deviceId,
							Timestamp = record.TimeStamp,
							IngestTimestamp = DateTime.Now,
							Sensors = GetSensor(record.measures, twin, record.TimeStamp)
						};
						results.Add(measurement);
					}
				}
			}
		}

		private List<SensorMeasurement> GetSensor(List<Models.Value> measures, Twin twin, DateTimeOffset timestamp)
		{
			var twinSensors = twin.Tags["sensors"];
			var ret = new List<SensorMeasurement>();
			if (twinSensors is null)
				return ret;
			foreach (var sensorValues in measures)
			{
				for (int i = 0; i < sensorValues.ids.Length; i++)
				{
					var sensorId = $"{sensorValues.sn}_{sensorValues.ids[i]}";
					var twinProperties = twinSensors[sensorId];
					if (twinProperties is null)
					{
						_logger.LogWarning($"Twin properties for sensorId {sensorId} not found.");
						continue;
					}
					int[] units = GetArrayFromTwin<int>(twinProperties["unit"]);
					int[] offsets = GetArrayFromTwin<int>(twinProperties["offset"]);
					double[] scales = GetArrayFromTwin<double>(twinProperties["scale"]);
					var channelId = sensorValues.ids[i];				
					var values =  units.Select((u, idx) => sensorValues.v[i] * scales[idx] + offsets[idx]).ToArray();
					var error = sensorValues.err[i];
					var measurement = new SensorMeasurement(
						SensorId: sensorId,
						SerialNumber: sensorValues.sn,
						Values: values,
						Raw: null, // Assuming you don't have raw data in this context
						ReadingTimestamp: timestamp, // You might want to adjust this based on your data
						MetadataRef: $"{twinProperties["uniqueId"]}"						
					);
					ret.Add(measurement);
				}
			}
			return ret;
		}

		private T?[] GetArrayFromTwin<T>(dynamic unitsObj)
		{
			T?[] ret = Array.Empty<T?>();

			if (unitsObj is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Array)
			{
				var list = new List<T?>();
				foreach (var el in je.EnumerateArray())
				{
					if (el.ValueKind == System.Text.Json.JsonValueKind.Number && el.TryGetInt32(out var v)) list.Add((T)Convert.ChangeType(v, typeof(T)));
					else if (el.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(el.GetString(), out var vi)) list.Add((T)Convert.ChangeType(vi, typeof(T)));
				}
				ret = list.ToArray();
			}
			else if (unitsObj is Newtonsoft.Json.Linq.JArray jarr)
			{
				if (typeof(T) == typeof(double))
				{
					ret = jarr.Select(t => t.Type == JTokenType.Float
											? (T)Convert.ChangeType(t.Value<double>(), typeof(T))
											: (double.TryParse(t.ToString().Replace(',', '.'), out var x)
												? (T)Convert.ChangeType(x, typeof(T)) : default)).ToArray();
				}
				else
					ret = jarr.Select(t => t.Type == JTokenType.Integer
											? (T)Convert.ChangeType(t.Value<int>(), typeof(T))
											: (int.TryParse(t.ToString(), out var x)
												? (T)Convert.ChangeType(x, typeof(T)) : default)).ToArray();
			}
			else if (unitsObj is System.Collections.IEnumerable ie)
			{
				ret = ie.Cast<object>().Select(o => (T)Convert.ChangeType(o, typeof(T))).ToArray();
			}
			//else if (unitsObj != null && int.TryParse(unitsObj.ToString(), out var single))
			//{
			//	ret = new[] { (T)Convert.ChangeType(single, typeof(T)) };
			//}
			return ret;
		}
	}
}