using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace BackendIotGigi.Models
{
	public class HD35ConfigPayload : PayloadBase
	{

		#region Private Variables
		#endregion Private Variables

		#region Constructors
		public HD35ConfigPayload()
		{
			Type = "config";
		}
		#endregion Constructors

		#region Properties
		[JsonPropertyName("ts")]
		[JsonConverter(typeof(DatetimeConverter))]
		public DateTime Timestamp { get; set; } = DateTime.UtcNow;
		[JsonPropertyName("fw_release")]
		public string FirmwareRelease { get; set; } = string.Empty;
		[JsonPropertyName("network")]
		public NetworkConfig Network { get; set; } = new();
		[JsonPropertyName("params")]
		public ParamConfig[] Params { get; set; } = Array.Empty<ParamConfig>();

		[JsonPropertyName("x509_thumbprint")]
		public string X509Thumbprint { get; set; } = string.Empty;
		#endregion Properties

		#region Public Methods
		#endregion Public Methods

		#region Private Methods
		#endregion Private Methods

		#region Events
		#endregion Events

	}

	public class NetworkConfig
	{
		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;
		[JsonPropertyName("devices")]
		public string[] Devices { get; set; } = Array.Empty<string>();
	}

	public class ParamConfig
	{
		[JsonPropertyName("sn")]
		public string SerialNumber { get; set; } = string.Empty;
		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;
		[JsonPropertyName("qual")]
		public int Qual { get; set; }
		[JsonPropertyName("label")]
		public string Label { get; set; } = string.Empty;
		[JsonPropertyName("sublabel")]
		public string SubLabel { get; set; } = string.Empty;
		[JsonPropertyName("unit")]
		public int[] Unit { get; set; } = Array.Empty<int>();
		[JsonPropertyName("format")]
		public string[] Format { get; set; } = Array.Empty<string>();
		[JsonPropertyName("offset")]
		public int[] Offset { get; set; } = Array.Empty<int>();
		[JsonPropertyName("scale")]
		public double[] Scale { get; set; } = Array.Empty<double>();
	}
}