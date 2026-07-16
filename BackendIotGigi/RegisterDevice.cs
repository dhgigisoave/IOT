using BackendIotGigi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Core;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Configuration;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BackendIotGigi;

public class RegisterDevice
{
	private readonly ILogger<RegisterDevice> _logger;
	private readonly IConfiguration _config;

	public RegisterDevice(ILogger<RegisterDevice> logger, IConfiguration config)
	{
		_logger = logger;
		_config = config;
	}

	[Function("RegisterDevice")]
	public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "registerdevice")] HttpRequest req)
	{
		try
		{

			_logger.LogInformation("C# HTTP trigger function processed a request.");
			string body;
			using (var reader = new StreamReader(req.Body, Encoding.UTF8))
			{
				body = (await reader.ReadToEndAsync())?.Trim() ?? string.Empty;
			}

			if (string.IsNullOrEmpty(body))
				return new BadRequestObjectResult("Body vuoto. Invia deviceId (JSON { \"deviceId\": \"...\" } o solo testo).");
			var config = JsonSerializer.Deserialize<HD35ConfigPayload>(body);
			if (config is null || string.IsNullOrEmpty(config.Id))
				return new BadRequestObjectResult("Invalid payload. Invia deviceId (JSON { \"deviceId\": \"...\" } o solo testo).");
			var iotHubHostName = _config.GetValue<string>("IoTHubName") ?? string.Empty;
			var connectionString = _config.GetValue<string>("IoTHubConnectionString2") ?? string.Empty;
			if (string.IsNullOrEmpty(config.Id) || string.IsNullOrEmpty(iotHubHostName) || string.IsNullOrEmpty(connectionString))
			{
				_logger.LogError("Device ID, IoT Hub Host Name, or Connection String is missing.");
				return new BadRequestObjectResult("Device ID, IoT Hub Host Name, or Connection String is missing.");
			}

			var rm = RegistryManager.CreateFromConnectionString(connectionString);
			var device = await rm.GetDeviceAsync(config.Id);
			if (device == null)
			{
				device = await rm.AddDeviceAsync(new Device(config.Id));
			}
			if (device == null)
			{
				_logger.LogError($"Failed to register device: {config.Id}");
				return new BadRequestObjectResult($"Failed to register device: {config.Id}");
			}
			var deviceKey = device?.Authentication?.SymmetricKey?.PrimaryKey;
			if (string.IsNullOrEmpty(deviceKey))
				return new ObjectResult("Impossibile ottenere la device key.") { StatusCode = 500 };
			var sasToken = BuildDeviceSasToken(iotHubHostName, config.Id, deviceKey
				, TimeSpan.FromDays(365), out long expiresOn);

			await RegisterSensors(rm, config);

			return new OkObjectResult(sasToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "An error occurred while registering the device.");
			return new ObjectResult("An error occurred while registering the device.") { StatusCode = 500 };
		}
	}

	public static async Task<List<(string sensorId, string uniqueId)>> RegisterSensors(RegistryManager rm, HD35ConfigPayload config)
	{
		var twin = await rm.GetTwinAsync(config.Id);
		var ret = new List<(string sensorId, string uniqueId)>();
		if (twin is not null)
		{
			var reported = twin.Properties.Reported;
			// creare/aggiornare la struttura sensors nel reported
			var sensorsCollection = new TwinCollection();
			foreach (var sensor in config.Params)
			{
				var idSensor = $"{sensor.SerialNumber}_{sensor.Id}";
				var singleRet = (sensorId: idSensor, uniqueId: Guid.NewGuid().ToString());
				ret.Add(singleRet);
				var sTwin = new TwinCollection
				{
					["uniqueId"] = singleRet.uniqueId,
					["id"] = idSensor,
					["serialNumber"] = sensor.SerialNumber,
					["id_channel"] = sensor.Id,
					["qual"] = sensor.Qual,
					["label"] = sensor.Label,
					["sublabel"] = sensor.SubLabel,
					["unit"] = sensor.Unit,
					["format"] = sensor.Format,
					["offset"] = sensor.Offset,
					["scale"] = sensor.Scale,
					["timestamp"] = config.Timestamp
					// altri metadati
				};
				sensorsCollection[idSensor] = sTwin;
			}

			twin.Tags["sensors"] = sensorsCollection;

			try
			{
				await rm.UpdateTwinAsync(config.Id, twin, twin.ETag);
			}
			catch (Microsoft.Azure.Devices.Common.Exceptions.PreconditionFailedException)
			{
				// semplice retry: rileggi ed esegui di nuovo (o usa "*" per forzare)
				twin = await rm.GetTwinAsync(config.Id);
				twin.Tags["sensors"] = sensorsCollection;
				await rm.UpdateTwinAsync(config.Id, twin, twin.ETag);
			}
		}
		return ret;
	}

	private static string BuildDeviceSasToken(string iotHubHostName, string deviceId, string deviceKeyBase64, TimeSpan ttl, out long expiresOn)
	{
		// resourceUri è il Fully Qualified Resource per il device
		var resourceUri = $"{iotHubHostName}/devices/{deviceId}";
		expiresOn = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();

		var stringToSign = WebUtility.UrlEncode(resourceUri).ToLowerInvariant() + "\n" + expiresOn.ToString();
		var keyBytes = Convert.FromBase64String(deviceKeyBase64);

		using var hmac = new HMACSHA256(keyBytes);
		var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
		var signatureBase64 = Convert.ToBase64String(signature);
		var signatureEscaped = WebUtility.UrlEncode(signatureBase64);

		var sr = WebUtility.UrlEncode(resourceUri).ToLowerInvariant();

		return $"SharedAccessSignature sr={sr}&sig={signatureEscaped}&se={expiresOn}";
	}
}