using BackendIotGigi.Models;
using BackendIotGigi.Services;
using BackendIotGigi.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace BackendIotGigi;

public class GetDevicesForUser
{
	private readonly ILogger<GetDevicesForUser> _logger;
	private readonly IConfiguration _config;

	private const string TAG_CREATEDAT = "createdAt";
	private const string TAG_NAME = "name";
	private const string TAG_DESC = "description";
	private const string TAG_LOCATION = "location";
	private const string TAG_USERID = "userId";
	private const string TAG_OTP = "OTP";

	public GetDevicesForUser(ILogger<GetDevicesForUser> logger, IConfiguration config)
	{
		_logger = logger;
		_config = config;
	}

	[Function("GetDevicesForUser")]
	public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "devicesforuser")] HttpRequest req)
	{
		_logger.LogInformation("C# HTTP trigger function processed a request.");

		// Rispondi subito alla preflight
		if (req.Method == "OPTIONS")
		{
			return new OkResult();
		}

		var (rm, devForUser, connectionString) = await AzureConnect
			.GetRegistryManagerAndRequestAsync<DeviceForUserReq, GetDevicesForUser>(req, _config, _logger);

		if (rm is null || devForUser is null)
		{
			return new BadRequestObjectResult("Invalid payload");
		}

		var devFounds = rm.CreateQuery($"select * from devices where tags.{TAG_USERID} = '{devForUser.UserId}'")
			.GetNextAsTwinAsync()
			.Result;

		var temp = new List<HD35Device>();
		foreach( var twin in devFounds)
		{
			temp.Add(
				new()
				{
					Id = twin.DeviceId,
					CreatedAt = twin.Tags.Contains(TAG_CREATEDAT)
						? DateTime.Parse($"{twin.Tags[TAG_CREATEDAT]}") : DateTime.MinValue,
					Description = twin.Tags.Contains(TAG_DESC)
						? $"{twin.Tags[TAG_DESC]}" : string.Empty,
					Name = twin.Tags.Contains(TAG_NAME)
						? $"{twin.Tags[TAG_NAME]}" : string.Empty,
					Location = twin.Tags.Contains(TAG_LOCATION)
						? $"{twin.Tags[TAG_LOCATION]}" : string.Empty
				}
				);
		}

		return await Task.FromResult(new OkObjectResult(temp));
	}

	[Function("ClaimDevice")]
	public async Task<IActionResult> ClaimDevice([HttpTrigger(AuthorizationLevel.Function, "post", Route = "claimdevice")] HttpRequest req)
	{
		try
		{

			_logger.LogInformation("C# HTTP trigger function processed a request.");
			//var resp = AzureConnect.InitResponse(req);

			// Rispondi subito alla preflight
			if (req.Method == "OPTIONS")
			{
				return new OkResult();
			}

			var (rm, deviceClaim, connectionString) = await AzureConnect
				.GetRegistryManagerAndRequestAsync<DeviceClaimReq, GetDevicesForUser>(req, _config, _logger);
			if (deviceClaim is null || rm is null)
			{
				return new BadRequestObjectResult("Invalid payload. Invia deviceId (JSON { \"deviceId\": \"...\" } o solo testo).");
			}
			var devFound = rm.CreateQuery($"select * from devices where tags.OTP = '{deviceClaim.OTP}'")
				.GetNextAsTwinAsync()
				.Result;
			if (devFound is null || devFound.Count() == 0)
			{
				return new BadRequestObjectResult("Device Id not found");
			}
			if (devFound.Count() > 1)
			{
				return new BadRequestObjectResult("Multiple devices found with the same OTP. Please contact support.");
			}
			await AssegnaDeviceAtUser(deviceClaim.userId, rm, devFound.First());
			var response = new DeviceClaimResp(
				new HD35Device(
					id: devFound.First().DeviceId,
					name: devFound.First().Tags.Contains("name") 
						? devFound.First().Tags["name"].ToString() : string.Empty,
					description: devFound.First().Tags.Contains("description") 
						? devFound.First().Tags["description"].ToString() : string.Empty,
					location: devFound.First().Tags.Contains("location") 
						? devFound.First().Tags["location"].ToString() : string.Empty,
					createdAt: devFound.First().Tags.Contains("createdAt") ?
						DateTime.Parse(devFound.First().Tags["createdAt"].ToString()) : DateTime.MinValue
					),
					deviceClaim.userId
				);
		return new OkObjectResult(response);
		}
		catch (Exception e)
		{
			_logger.LogError(e, "An error occurred while claiming the device.");
			return new StatusCodeResult(StatusCodes.Status500InternalServerError);
		}
	}

	private async Task AssegnaDeviceAtUser(string userId, RegistryManager rm, Twin devFound)
	{
		try
		{
			devFound.Tags["userId"] = userId;
			await rm.UpdateTwinAsync(devFound.DeviceId, devFound, devFound.ETag);

		}
		catch (Exception)
		{

			throw;
		}
	}
}