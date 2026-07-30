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
using System.Security.Cryptography.X509Certificates;
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
	public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "options", Route = "registerdevice")] HttpRequest req)
	{
		try
		{
			_logger.LogInformation("C# HTTP trigger function processed a request.");

			// Aggiungi header CORS su tutte le risposte
			var resp = req.HttpContext.Response;
			resp.Headers["Access-Control-Allow-Origin"] = "*";
			resp.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
			resp.Headers["Access-Control-Allow-Headers"] = "Content-Type";

			// Rispondi subito alla preflight
			if (req.Method == "OPTIONS")
			{
				return new OkResult();
			}

			var iotHubHostName = _config.GetValue<string>("IoTHubName") ?? string.Empty;
			var (rm, config, connectionString) = Utility.AzureConnect
				.GetRegistryManagerAndRequestAsync<HD35ConfigPayload, RegisterDevice>(
				req, _config, _logger).Result;
			if (rm is null || config is null)
			{
				_logger.LogError("RegistryManager or config is null.");
				return new BadRequestObjectResult("RegistryManager or config is null.");
			}

			// Genera certificato self-signed e ottieni thumbprint
			using var cert = CreateSelfSignedCertificate(config.Id);
			var thumbprint = cert.Thumbprint ?? string.Empty;

			// Registra o aggiorna il device con X.509 thumbprint
			var device = await rm.GetDeviceAsync(config.Id);
			if (device == null)
			{
				device = new Device(config.Id)
				{
					Authentication = new AuthenticationMechanism
					{
						Type = AuthenticationType.SelfSigned,
						X509Thumbprint = new X509Thumbprint { PrimaryThumbprint = thumbprint }
					}
				};
				device = await rm.AddDeviceAsync(device);
			}
			else
			{
				device.Authentication = new AuthenticationMechanism
				{
					Type = AuthenticationType.SelfSigned,
					X509Thumbprint = new X509Thumbprint { PrimaryThumbprint = thumbprint }
				};
				device = await rm.UpdateDeviceAsync(device);
			}

			if (device == null)
			{
				_logger.LogError($"Failed to register device: {config.Id}");
				return new BadRequestObjectResult($"Failed to register device: {config.Id}");
			}

			// registra i sensori nel twin
			await RegisterSensors(rm, config);

			// Esporta il certificato PFX in base64 (il device lo userà per presentarsi)
			var pfxBytes = cert.Export(X509ContentType.Pfx);
			var pfxBase64 = Convert.ToBase64String(pfxBytes);

			// Risposta: host, deviceId, thumbprint, e certificato PFX (base64)
			return new OkObjectResult(new
			{
				iotHubHostName,
				deviceId = config.Id,
				x509Thumbprint = thumbprint,
				certificatePfxBase64 = pfxBase64
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "An error occurred while registering the device.");
			return new ObjectResult("An error occurred while registering the device.") { StatusCode = 500 };
		}
	}

	[Function("RegisterDevicePackaging")]
	public async Task<IActionResult> RegisterDevicePackaging([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "options"
		, Route = "registerdevicepackaging")] HttpRequest req)
	{
		try
		{
			_logger.LogInformation("C# HTTP trigger function processed a request.");

			// Aggiungi header CORS su tutte le risposte
			var resp = req.HttpContext.Response;
			resp.Headers["Access-Control-Allow-Origin"] = "*";
			resp.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
			resp.Headers["Access-Control-Allow-Headers"] = "Content-Type";

			// Rispondi subito alla preflight
			if (req.Method == "OPTIONS")
			{
				return new OkResult();
			}

			var iotHubHostName = _config.GetValue<string>("IoTHubName") ?? string.Empty;
			var (rm, payload, connectionString) = Utility.AzureConnect
				.GetRegistryManagerAndRequestAsync<PackagingPayload, RegisterDevice>(
				req, _config, _logger).Result;
			
			if (rm is null || payload is null)
			{
				_logger.LogError("RegistryManager or payload is null.");
				return new BadRequestObjectResult("RegistryManager or payload is null.");
			}

			var device = await rm.GetDeviceAsync(payload.SerialNumber);
			if (device == null)
			{
				device = new Device(payload.SerialNumber);
				device = await rm.AddDeviceAsync(device);
			}
			else
			{
				return new BadRequestObjectResult("Device already exists.");
			}
			if (device is not null)
			{
				var otp = await SetDevice(rm, payload);
				return new OkObjectResult(new
				{
					iotHubHostName,
					deviceId = payload.SerialNumber,
					otp
				});
			}
			else
			{
				_logger.LogError($"Failed to register device: {payload.SerialNumber}");
				return new BadRequestObjectResult($"Failed to register device: {payload.SerialNumber}");
			}
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
			twin.Tags["firmware_ver"] = config.FirmwareRelease;
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
	public static async Task<string> SetDevice(RegistryManager rm, PackagingPayload infos)
	{
		var twin = await rm.GetTwinAsync(infos.SerialNumber);
		var ret = GeneraOTP(infos.SerialNumber);
		if (twin is not null)
		{
			twin.Tags["OTP"] = ret;
			twin.Tags["location"] = infos.Location;
			twin.Tags["name"] = infos.Name;
			twin.Tags["description"] = infos.Description;
			twin.Tags["createdAt"] = DateTime.UtcNow;

			try
			{
				await rm.UpdateTwinAsync(infos.SerialNumber, twin, twin.ETag);
			}
			catch (Microsoft.Azure.Devices.Common.Exceptions.PreconditionFailedException)
			{
				// semplice retry: rileggi ed esegui di nuovo (o usa "*" per forzare)
				twin = await rm.GetTwinAsync(infos.SerialNumber);
				twin.Tags["OTP"] = ret;
				await rm.UpdateTwinAsync(infos.SerialNumber, twin, twin.ETag);
			}
		}
		return ret;
	}

	private static string GeneraOTP(string serialNumber)
	{
		// Genera un OTP basato su serialNumber e timestamp
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var otp = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{serialNumber}:{timestamp}"));
		return otp;
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

	// Genera un certificato self-signed con chiave esportabile
	private static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
	{
		using var rsa = RSA.Create(2048);
		var req = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
		req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
		req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

		var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
		var notAfter = notBefore.AddYears(1);

		using var cert = req.CreateSelfSigned(notBefore, notAfter);
		// Re-import as PFX exportable to ensure private key è esportabile
		var pfx = cert.Export(X509ContentType.Pfx);
		return new X509Certificate2(pfx, (string?)null, X509KeyStorageFlags.Exportable);
	}
}