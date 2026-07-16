using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace BackendIotGigi.Models
{
	public class PayloadBase
	{
		[JsonPropertyName("api-key")]
		public string ApiKey { get; set; } = string.Empty;

		public string Type { get; set; } = string.Empty;

		[JsonPropertyName("id")]
		public string Id { get; set; } = Guid.NewGuid().ToString();
	}
}
