using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackendIotGigi.Models
{
	public record SensorMetadata(
		[property: JsonPropertyName("displayName")] string? DisplayName,
		[property: JsonPropertyName("id")] string? Id,
		[property: JsonPropertyName("serialNumber")] string? SerialNumber,
		[property: JsonPropertyName("id_channel")] string? IdChannel,
		[property: JsonPropertyName("qual")] double? Qual,
		[property: JsonPropertyName("unit")] int[]? Unit,
		[property: JsonPropertyName("format")] string[]? Format,
		[property: JsonPropertyName("offset")] int[]? Offset,
		[property: JsonPropertyName("scale")] double[]? Scale,
		[property: JsonPropertyName("timestamp")] DateTime? Timestamp,
		[property: JsonPropertyName("Type")] string? Type = "sensorMetadata"
	);

	public record SensorMeasurement(
		[property: JsonPropertyName("sensorId")] string SensorId,
		[property: JsonPropertyName("serialNumber")] string? SerialNumber,
		[property: JsonPropertyName("values")] double[]? Values,
		[property: JsonPropertyName("raw")] JsonElement? Raw,
		[property: JsonPropertyName("readingTimestamp")] DateTimeOffset ReadingTimestamp,
		[property: JsonPropertyName("metadataRef")] string MetadataRef
	)
	{
		public SensorMeasurement() : this("", null, null, null, DateTimeOffset.UtcNow, null) { }
	}

	public record MeasurementSnapshot(
		[property: JsonPropertyName("id")] string Id,
		[property: JsonPropertyName("Type")] string Type, // "measurementSnapshot"
		[property: JsonPropertyName("deviceId")] string DeviceId,
		[property: JsonPropertyName("configVersionId")] string? ConfigVersionId,
		[property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,      // momento della misura aggregata
		[property: JsonPropertyName("ingestTimestamp")] DateTimeOffset IngestTimestamp,
		[property: JsonPropertyName("sensors")] List<SensorMeasurement> Sensors,
		[property: JsonPropertyName("rawPayload")] JsonElement? RawPayload
	)
	{
		public MeasurementSnapshot() : this(Guid.NewGuid().ToString(), "measurementSnapshot", ""
			, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new List<SensorMeasurement>(), null) { }
	}
}