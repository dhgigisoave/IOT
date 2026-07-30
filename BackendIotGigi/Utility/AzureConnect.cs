using BackendIotGigi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace BackendIotGigi.Utility
{
	internal static class AzureConnect
	{
		public static async Task<(RegistryManager? rm, T? req, string connectionString)> GetRegistryManagerAndRequestAsync<T, U>(
			HttpRequest req
			, IConfiguration config,
			ILogger<U> _logger) where T : class
		{
			(RegistryManager? rm, T? req, string connectionString) ret =  default;
			string body;
			using (var reader = new StreamReader(req.Body, Encoding.UTF8))
			{
				body = (await reader.ReadToEndAsync())?.Trim() ?? string.Empty;
			}

			if (string.IsNullOrEmpty(body))
				throw new BadHttpRequestException("Body vuoto. Invia deviceId (JSON { \"deviceId\": \"...\" } o solo testo).");

			var obj = JsonSerializer.Deserialize<T?>(body);
			if (obj is null )
				throw new BadHttpRequestException("Invalid payload.");

			ret.connectionString = config.GetValue<string>("IoTHubConnectionString2") ?? string.Empty;

			ret.rm = RegistryManager.CreateFromConnectionString(ret.connectionString);
			ret.req = obj;
			return ret;
		}

		public static HttpResponse InitResponse(HttpRequest req)
		{
			// Aggiungi header CORS su tutte le risposte
			var resp = req.HttpContext.Response;
			resp.Headers["Access-Control-Allow-Origin"] = "*";
			resp.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
			resp.Headers["Access-Control-Allow-Headers"] = "Content-Type";
			return resp;
		}
	}
}
