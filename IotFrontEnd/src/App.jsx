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

function isoLabel(ts) {
	const d = new Date(ts);
	if (isNaN(d)) return ts?.toString() ?? '';
	return d.toISOString().replace('T', ' ').replace('Z', '');
}

function buildChartData(measurements) {
	if (!Array.isArray(measurements) || measurements.length === 0) return { labels: [], datasets: [] };

	// Raggruppa per sensorId
	const groups = {};
	for (const m of measurements) {
		const sid = m.sensorId ?? m.SensorId ?? 'unknown';
		const timestamp = m.timestamp ?? m.Timestamp ?? m.timestampISO ?? m.timestampUtc;
		const value = m.readingValue ?? m.ReadingValue ?? m.value ?? m.dati ?? m.misura;
		if (!groups[sid]) groups[sid] = [];
		groups[sid].push({ timestamp, value });
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

function App() {
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState(null);
	const [measurementsRaw, setMeasurementsRaw] = useState([]); // tutti i dati
	const [selectedDevice, setSelectedDevice] = useState('all');
	const [devices, setDevices] = useState([]);

	useEffect(() => {
		let mounted = true;
		(async () => {
			try {
				const res = await backend.getDataFromCosmosDb();
				if (!mounted) return;
				const measurements = Array.isArray(res) ? res : (res ? [res] : []);
				setMeasurementsRaw(measurements);

				// estrai deviceId unici
				const deviceSet = new Set();
				for (const m of measurements) {
					const did = m.sensorId ?? m.sensorId ?? 'unknown';
					deviceSet.add(did);
				}
				const deviceList = Array.from(deviceSet);
				setDevices(deviceList);
				// default selezionato: "all" oppure primo device
				setSelectedDevice(deviceList.length > 0 ? deviceList[0] : 'all');

			} catch (e) {
				console.error(e);
				setError(e.message ?? String(e));
			} finally {
				if (mounted) setLoading(false);
			}
		})();
		return () => { mounted = false; };
	}, []);

	// derived filtered measurements
	const filteredMeasurements = useMemo(() => {
		return selectedDevice === 'all'
			? measurementsRaw
			: measurementsRaw.filter(m => (m.sensorId ?? m.sensorId) === selectedDevice);
	}, [selectedDevice, measurementsRaw]);

	// derived chart data (memorized)
	const chartData = useMemo(() => buildChartData(filteredMeasurements), [filteredMeasurements]);


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
			</div>

			{loading && <p>Caricamento dati...</p>}
			{error && <p style={{ color: 'red' }}>Errore: {error}</p>}
			{!loading && chartData.datasets.length === 0 && <p>Nessuna misura disponibile</p>}
			{!loading && chartData.datasets.length > 0 && (
				<Line
					data={chartData}
					options={{
						responsive: true,
						plugins: {
							title: { display: true, text: 'Andamento misure per sensore' },
							legend: { position: 'bottom' }
						},
						scales: {
							x: { display: true, title: { display: false } },
							y: { display: true, title: { display: true, text: 'Valore' } }
						}
					}}
				/>
			)}
		</div>
	)
}

export default App