using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace BackendIotGigi.Models
{
	public class PackagingPayload : PayloadBase
	{
		[JsonPropertyName("serial_number")]
		public string SerialNumber { get; set; } = string.Empty;
		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;
		[JsonPropertyName("description")]
		public string Description { get; set; } = string.Empty;
		[JsonPropertyName("location")]
		public string Location { get; set; } = string.Empty;

	}
}
