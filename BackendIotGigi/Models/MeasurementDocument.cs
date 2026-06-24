using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackendIotGigi.Models
{
	public record Coordinates(
	   [property: JsonPropertyName("x")] double X,
	   [property: JsonPropertyName("y")] double Y
   );

	public record MeasurementDocument(
		[property: JsonPropertyName("id")] string Id,
		[property: JsonPropertyName("docType")] string DocType, // "measurement"
		[property: JsonPropertyName("sensorId")] string SensorId,
		[property: JsonPropertyName("deviceId")] string? DeviceId,
		[property: JsonPropertyName("sensorType")] string SensorType,
		[property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
		[property: JsonPropertyName("readingValue")] double ReadingValue,
		[property: JsonPropertyName("readingUnit")] string ReadingUnit,
		[property: JsonPropertyName("coordinates")] Coordinates? Coordinates,
		[property: JsonPropertyName("raw")] JsonElement? Raw,
		[property: JsonPropertyName("ingestTimestamp")] DateTimeOffset IngestTimestamp
	)
	{
		public MeasurementDocument() : this(Guid.NewGuid().ToString(), "measurement", "", null, "", DateTimeOffset.UtcNow, 0.0, "", null, null, DateTimeOffset.UtcNow)
		{ }
		public MeasurementDocument(string id, string sensorId, string? deviceId, string sensorType,
			DateTimeOffset timestamp, double readingValue, string readingUnit,
			Coordinates? coordinates, JsonElement? raw, DateTimeOffset ingestTimestamp)
			: this(id, "measurement", sensorId, deviceId, sensorType, timestamp, readingValue, readingUnit, coordinates, raw, ingestTimestamp)
		{ }
	}
}
