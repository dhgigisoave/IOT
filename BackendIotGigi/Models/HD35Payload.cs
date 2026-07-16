using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackendIotGigi.Models
{
	public class HD35Payload : PayloadBase
	{

		#region Private Variables
		#endregion Private Variables

		#region Constructors
		public HD35Payload()
		{
			Type = "data";
		}
		#endregion Constructors

		#region Properties
		[JsonPropertyName("data")]
		public Data[] Data { get; set; } = Array.Empty<Data>();
		#endregion Properties

		#region Public Methods
		#endregion Public Methods

		#region Private Methods
		#endregion Private Methods

		#region Events
		#endregion Events

	}

	public class Data
	{
		[JsonConverter(typeof(DatetimeConverter))]
		[JsonPropertyName("ts")]
		public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
		[JsonPropertyName("values")]
		public List<Value> measures { get; set; } = new ();
	}

	public class Value
	{
		public string sn { get; set; } = string.Empty;
		public string[] ids { get; set; } = Array.Empty<string>();
		public double[] v { get; set; } = Array.Empty<double>();
		public string[] err { get; set; } = Array.Empty<string>();
	}
}
