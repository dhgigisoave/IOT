import { useState, useEffect, useMemo } from 'react'
import {
	Chart as ChartJS,
	CategoryScale,
	LinearScale,
	PointElement,
	LineElement,
	Title,
	Tooltip,
	Legend,
} from 'chart.js';
import { Line } from "react-chartjs-2";
import './App.css'
import * as backend from './services/backend.js'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend);

const _unitsLabel = [{ id: 0, label: "C°" }, { id: 1, label: "°F" }];

function isoLabel(ts) {
	const d = new Date(ts);
	if (isNaN(d)) return ts?.toString() ?? '';
	return d.toISOString().replace('T', ' ').replace('Z', '');
}

/*
Guarda tutte le misure e restituisce per ogni sensore la lista delle unità di misura uniche. 
Per ogni misura, per ogni sensore , prendi la proprietà Metadata.Unit e aggiungila a un Set. Alla fine, restituisci un dictionary (sensore, lista unità di misura uniche).
*/
function getUnityForSensor(measurements) {
	const unityDict = new Set();
	for (const m of measurements) {
		for (const sensor of m.Sensors) {
			const sid = sensor.Measurement.sensorId;
			const unit = sensor.Metadata?.unit;
			if (Array.isArray(unit)) {
				if (!unityDict[sid]) unityDict[sid] = new Set();
				for (const u of unit) {
					const label = _unitsLabel.find((l) => l.id === u);
					if (!unityDict[sid].has(label.id))
						unityDict[sid].add(label);
				}
			}
		}
	}
	// Convert sets to arrays
	for (const sid in unityDict) {
		unityDict[sid] = Array.from(unityDict[sid]);
	}
	return unityDict;
}

function buildChartData(measurements, selectedUnity) {
	if (!Array.isArray(measurements) || measurements.length === 0) return { labels: [], datasets: [] };

	// Raggruppa per sensorId
	const groups = {};
	for (const m of measurements) {
		for (const s of m.Sensors) {
			const sid = s.Measurement.sensorId;
			const timestamp = s.Measurement.readingTimestamp;
			const value = s.Measurement.values[(selectedUnity[sid]?.id) ?? 0];
			if (!groups[sid]) groups[sid] = [];
			groups[sid].push({ timestamp, value });
		}
	}

	// raccolta di tutte le etichette uniche e ordinate
	const labelSet = new Set();
	for (const sid of Object.keys(groups)) {
		for (const it of groups[sid]) {
			labelSet.add(new Date(it.timestamp).toISOString());
		}
	}
	const labels = Array.from(labelSet).sort().map(isoLabel);

	// palette semplice
	const colors = [
		'rgba(75,192,192,1)',
		'rgba(255,99,132,1)',
		'rgba(54,162,235,1)',
		'rgba(255,159,64,1)',
		'rgba(153,102,255,1)',
		'rgba(201,203,207,1)'
	];

	const datasets = Object.keys(groups).map((sid, idx) => {
		if (!selectedUnity || Object.keys(selectedUnity).length === 0) return null;
		const map = new Map(groups[sid].map(x => [new Date(x.timestamp).toISOString(), Number(x.value)]));
		const labelArray = Array.from(labelSet);
		const data = labelArray.map(l => {
			//const iso = new Date(l).toISOString();
			const v = map.get(l);
			return (v === undefined || Number.isNaN(v)) ? null : v;
		});
		return {
			label: sid,
			data,
			borderColor: colors[idx % colors.length],
			backgroundColor: colors[idx % colors.length].replace('1)', '0.2)'),
			tension: 0.2,
			fill: false,
			pointRadius: 2
		};
	});

	return { labels, datasets };
}

function getFilteredMeasurement(measurementsRaw, selectedDevice) {
	let ret = measurementsRaw;
	if (selectedDevice === 'all') return ret;

	const sensorId = selectedDevice; // oppure la variabile che contiene l'id del sensore
	ret = measurementsRaw.map(m => {
		const sensors = Array.isArray(m.Sensors) ? m.Sensors.filter(s => s.Metadata.displayName === sensorId) : [];
		return { ...m, Sensors: sensors };
	});
	return ret;
}

function App() {
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState(null);
	const [measurementsRaw, setMeasurementsRaw] = useState([]); // tutti i dati
	const [selectedDevice, setSelectedDevice] = useState('all');
	const [devices, setDevices] = useState([]);
	const [units, setUnits] = useState([]);
	const [selectedUnity, setSelectedUnity] = useState([]);
	const [lastUpdate, setLastUpdate] = useState(null);

	function Refresh() {
		let mounted = true;
		(async () => {
			try {
				//const start = lastUpdate ? new Date(lastUpdate) : new Date(Date.now() - 10 * 60 * 1000);
				const start = new Date(2026, 6, 8); // ultimi 10 minuti
				const end = new Date();
				const res = await backend.getDataFromCosmosDb(start, end);
				if (!mounted) return;
				const measurements = Array.isArray(res) ? res : (res ? [res] : []);
				setMeasurementsRaw(measurements);
				setLastUpdate(end);

				// estrai deviceId unici
				const deviceSet = new Set();
				for (const m of measurements) {
					for (const sensor of m.Sensors) {
						if (sensor && sensor.Metadata) {
							const did = sensor.Metadata.displayName
							deviceSet.add(did);
						}
					}
				}
				const deviceList = Array.from(deviceSet);
				setDevices(deviceList);
				// default selezionato: "all" oppure primo device
				setSelectedDevice(deviceList.length > 0 ? deviceList[0] : 'all');
				const unitsTemp = getUnityForSensor(measurements);
				setUnits(unitsTemp);
				const selectedUnityDefault = new Set();
				for (const u in unitsTemp) {
					selectedUnityDefault[u] = unitsTemp[u][0];
				}
				setSelectedUnity(selectedUnityDefault);
			} catch (e) {
				console.error(e);
				setError(e.message ?? String(e));
			} finally {
				if (mounted) setLoading(false);
			}
		})();
		return () => { mounted = false; };
	}

	useEffect(() => { Refresh() }, []);

	// derived filtered measurements
	const filteredMeasurements = useMemo(() => getFilteredMeasurement(measurementsRaw, selectedDevice), [selectedDevice, measurementsRaw]);

	// derived chart data (memorized)
	const chartData = useMemo(() => buildChartData(filteredMeasurements, selectedUnity), [filteredMeasurements, selectedUnity]);


	return (
		<div className="App">
			<h3>Misure IoT</h3>

			{/* Selettore device */}
			<div style={{ marginBottom: 12 }}>
				<label style={{ marginRight: 8 }}>Seleziona device:</label>
				<select value={selectedDevice} onChange={e => setSelectedDevice(e.target.value)}>
					<option value="all">Tutti i device</option>
					{devices.map(d => (
						<option key={d} value={d}>{d}</option>
					))}
				</select>
				<button
					onClick={() => Refresh()}
				>Refresh</button>
			</div>

			{loading && <p>Caricamento dati...</p>}
			{error && <p style={{ color: 'red' }}>Errore: {error}</p>}
			{!loading && chartData.datasets.length === 0 && <p>Nessuna misura disponibile</p>}
			{!loading && chartData.datasets.length > 0 && (
				<div>
					{chartData.datasets.map((ds, i) => {
						const singleDataset = { ...ds, label: ds.label ?? `Sensor ${i}` };
						const dataForSingle = {
							labels: chartData.labels,
							datasets: [singleDataset]
						};
						return (
							<>
								<div>
									<label>Unità di misura</label>
									<select
										value={selectedUnity[ds.label]?.label ?? ''}
										onChange={e => setSelectedUnity(prev => ({
											...prev,
											[ds.label]: units[ds.label].find(u => u.label === e.target.value)
										}))}
									>
										{units[singleDataset.label]?.map(u => (
											<option key={u.id} value={u.label}>{u.label}</option>
										))}
									</select>
								</div>
								<div key={singleDataset.label} style={{ marginBottom: 18 }}>
									<Line
										data={dataForSingle}
										options={{
											responsive: true,
											plugins: {
												title: { display: true, text: singleDataset.label },
												legend: { display: false }
											},
											scales: {
												x: { display: true, title: { display: false } },
												y: { display: true, title: { display: true, text: 'Valore' } }
											}
										}}
									/>
								</div>
							</>);
					})}
				</div>
			)}
			<div>
				<label>{lastUpdate && lastUpdate.toString()}</label>
			</div>
		</div>
	)
}

export default App