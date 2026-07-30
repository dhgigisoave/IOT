using BackendIotGigi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;

namespace BackendIotGigi.Utility
{
	internal static class AzureConnect
	{
		private const string TenantId = "46ce2e07-99e1-43cc-b7d7-17bf92113dc0";
		private const string ClientId = "14f20a85-5b11-4d49-af02-e9bd1b64a489"; // backend client id (audience)

		// ConfigurationManager scarica e cachea le chiavi pubbliche da Azure AD
		private static readonly ConfigurationManager<OpenIdConnectConfiguration> _oidcConfigManager = new(
			$"https://login.microsoftonline.com/{TenantId}/v2.0/.well-known/openid-configuration",
			new OpenIdConnectConfigurationRetriever());

		public static async Task<(T? req, string connectionString)> GetRegistryManagerAndRequestAsync<T, U>(
			HttpRequest req,
			IConfiguration config,
			ILogger<U> _logger) where T : class
		{
			(T? req, string connectionString) ret = default;
			string body;
			using (var reader = new StreamReader(req.Body, Encoding.UTF8))
			{
				body = (await reader.ReadToEndAsync())?.Trim() ?? string.Empty;
			}

			if (string.IsNullOrEmpty(body))
				throw new BadHttpRequestException("Body vuoto.");

			var obj = JsonSerializer.Deserialize<T?>(body);
			if (obj is null)
				throw new BadHttpRequestException("Invalid payload.");

			ret.connectionString = config.GetValue<string>("IoTHubConnectionString2") ?? string.Empty;
			ret.req = obj;
			return ret;
		}

		public static HttpResponse InitResponse(HttpRequest req)
		{
			var resp = req.HttpContext.Response;
			resp.Headers["Access-Control-Allow-Origin"] = "*";
			resp.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
			resp.Headers["Access-Control-Allow-Headers"] = "Content-Type";
			return resp;
		}

		internal static async Task<string> ValidateTokenAsync(HttpRequest req)
		{
			var authHeader = req.Headers["Authorization"].FirstOrDefault();
			var token = authHeader?.StartsWith("Bearer ") == true ? authHeader[7..] : null;

			if (string.IsNullOrEmpty(token))
				throw new UnauthorizedAccessException("Token mancante.");

			// Scarica (o usa cache) delle chiavi pubbliche Azure AD
			var oidcConfig = await _oidcConfigManager.GetConfigurationAsync();

			var validationParams = new TokenValidationParameters
			{
				ValidateIssuer = true,
				ValidIssuers = [
					$"https://login.microsoftonline.com/{TenantId}/v2.0",
					$"https://sts.windows.net/{TenantId}/"
				],
				ValidateAudience = true,
				ValidAudiences = [ClientId, $"api://{ClientId}"],
				ValidateLifetime = true,
				ValidateIssuerSigningKey = true,
				IssuerSigningKeys = oidcConfig.SigningKeys
			};

			var handler = new JwtSecurityTokenHandler();
			handler.InboundClaimTypeMap.Clear(); // disabilita la rimappatura XML

			var principal = handler.ValidateToken(token, validationParams, out _);

			//var userId = principal.Identity?.Name  // già mappato correttamente
			//?? principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
			var userId = principal.FindFirst("preferred_username")?.Value
				  ?? principal.FindFirst("upn")?.Value
				  ?? principal.FindFirst("email")?.Value;

			if (string.IsNullOrEmpty(userId))
				throw new UnauthorizedAccessException("UserId non trovato nel token.");

			return userId;
		}
	}
}