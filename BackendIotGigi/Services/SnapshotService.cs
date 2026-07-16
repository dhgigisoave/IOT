using BackendIotGigi.Models;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BackendIotGigi.Services
{
	// DTO result: snapshot arricchito con metadata per sensore
	public class EnrichedSensorMeasurement
	{
		public SensorMeasurement? Measurement { get; init; }
		public SensorMetadata? Metadata { get; set; }
	}

	public class EnrichedMeasurementSnapshot
	{
		//public MeasurementSnapshot? Snapshot { get; init; }
		public List<EnrichedSensorMeasurement> Sensors { get; init; } = new();
	}

	public class SnapshotService
	{
		private readonly Container _container;

		public SnapshotService(CosmosClient cosmosClient, string databaseId, string containerId)
		{
			_container = cosmosClient.GetDatabase(databaseId).GetContainer(containerId);
		}

		// Recupera gli ultimi snapshot e li arricchisce con metadata (client-side join)
		public async Task<List<EnrichedMeasurementSnapshot>> GetLatestEnrichedSnapshotsAsync(DateTime start, DateTime end)
		{
			var sql = $"SELECT * FROM c WHERE c.Type = 'measurementSnapshot' AND c.timestamp >= @start AND c.timestamp <= @end ORDER BY c.timestamp DESC";
			var iter = _container.GetItemQueryIterator<MeasurementSnapshot>(new QueryDefinition(sql).WithParameter("@start", start).WithParameter("@end", end));
			var snapshots = new List<MeasurementSnapshot>();
			while (iter.HasMoreResults)
			{
				var page = await iter.ReadNextAsync();
				snapshots.AddRange(page);
			}

			if (!snapshots.Any()) return new List<EnrichedMeasurementSnapshot>();

			// Raccogli id da cercare: metadataRef dei sensori + configVersionId
			var metadataIds = snapshots
				.SelectMany(s => s.Sensors.Select(x => x.MetadataRef))
				.Where(id => !string.IsNullOrEmpty(id))
				.Distinct()
				.ToList();

			var configIds = snapshots
				.Select(s => s.ConfigVersionId)
				.Where(id => !string.IsNullOrEmpty(id))
				.Distinct()
				.ToList();

			var idsToFetch = metadataIds.Union(configIds).Distinct().ToList();

			// Se non ci sono metadata da recuperare ritorna snapshot vuoti di metadata
			Dictionary<string, JsonElement> metaDocs = new();
			if (idsToFetch.Any())
			{
				// costruisce la lista di parametri per IN(...)
				var inParams = string.Join(", ", idsToFetch.Select((_, i) => $"@id{i}"));
				var metaSql = $"SELECT * FROM c WHERE c.id IN ({inParams}) AND (c.Type = 'sensorMetadata' OR c.Type = 'deviceConfig' OR c.Type = 'sensorConfig')";
				var qd = new QueryDefinition(metaSql);
				for (int i = 0; i < idsToFetch.Count; i++) qd.WithParameter($"@id{i}", idsToFetch[i]);

				var metaIter = _container.GetItemQueryIterator<SensorMetadata>(qd);
				while (metaIter.HasMoreResults)
				{
					var page = await metaIter.ReadNextAsync();
					foreach (var el in page)
					{
						var id = el.Id ?? string.Empty;
						if (!string.IsNullOrEmpty(id) && !metaDocs.ContainsKey(id))
							metaDocs[id] = JsonSerializer.SerializeToElement(el);
					}
				}
			}

			// Costruisci i risultati arricchiti
			var results = new List<EnrichedMeasurementSnapshot>(snapshots.Count);
			foreach (var snap in snapshots)
			{
				var enriched = new EnrichedMeasurementSnapshot ();
				foreach (var s in snap.Sensors)
				{
					var em = new EnrichedSensorMeasurement { Measurement = s };
					// 1) prova metadataRef diretto
					if (!string.IsNullOrEmpty(s.MetadataRef) && metaDocs.TryGetValue(s.MetadataRef, out var doc))
					{
						em.Metadata = ExtractSensorMetadataFromDoc(doc, s.SensorId);
					}
					// 2) fallback su configVersionId (device config che contiene mappa sensors)
					else if (!string.IsNullOrEmpty(snap.ConfigVersionId) && metaDocs.TryGetValue(snap.ConfigVersionId, out var cfgDoc))
					{
						em.Metadata = ExtractSensorMetadataFromDoc(cfgDoc, s.SensorId);
					}
					// 3) se nulla rimane null (tu puoi decidere di denormalizzare qui)
					enriched.Sensors.Add(em);
				}
				results.Add(enriched);
			}

			return results;
		}

		// Estrae SensorMetadata da un documento che può essere:
		// - documento singolo sensorMetadata (con campi displayName, unit, ecc.)
		// - documento deviceConfig che contiene una proprietà "sensors" oggetto mappa sensorId -> metadata
		private SensorMetadata? ExtractSensorMetadataFromDoc(JsonElement doc, string sensorId)
		{
			try
			{
				// se è un deviceConfig con "sensors" object
				if (doc.TryGetProperty("sensors", out var sensorsElem) && sensorsElem.ValueKind == JsonValueKind.Object)
				{
					if (sensorsElem.TryGetProperty(sensorId, out var perSensor))
					{
						return JsonSerializer.Deserialize<SensorMetadata>(perSensor.GetRawText());
					}
					// talvolta la mappa potrebbe essere keyed con serialNumber oppure altro: tenta il primo elemento
					foreach (var prop in sensorsElem.EnumerateObject())
					{
						// se il sensorId compare come sottchiave, prova a deserializzare il valore
						var candidate = JsonSerializer.Deserialize<SensorMetadata>(prop.Value.GetRawText());
						if (candidate is not null && (candidate.Id == sensorId || candidate.SerialNumber == sensorId))
							return candidate;
					}
				}

				// altrimenti prova a deserializzare l'intero documento come SensorMetadata
				return JsonSerializer.Deserialize<SensorMetadata>(doc.GetRawText());
			}
			catch
			{
				// in caso di parsing fallito ritorna null
				return null;
			}
		}
	}
}