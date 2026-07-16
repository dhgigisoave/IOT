using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackendIotGigi.Models
{
	public class DatetimeConverter : JsonConverter<DateTime>
	{
		public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var ret = DateTime.Now;
			try
			{
				var ts = reader.GetInt64();
				ret = DateTime.UnixEpoch + TimeSpan.FromSeconds(ts);
			}
			catch (Exception)
			{
			}
			return ret;
		}

		public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
		{
			writer.WriteNumberValue((value - DateTime.UnixEpoch).TotalSeconds);
		}
	}

}
