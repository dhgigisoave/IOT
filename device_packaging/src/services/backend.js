export async function setNewDevice(seriale) {
	//const url = 'http://localhost:6280/api/registerdevicepackaging';
	const url = 'https://gigi-backend-e3bff8d7cwc4cyh4.eastus-01.azurewebsites.net/registerdevicepackaging';
	console.log('POST', url, 'seriale=', seriale);

	try {
		const res = await fetch(url, {
			method: "POST",
			//headers: { "Content-Type": "application/json" },
			body: JSON.stringify({ SerialNumber: seriale })
		});

		console.log('Fetch completed, status:', res.status, 'ok:', res.ok);
		console.log('Response headers:', [...res.headers.entries()]);

		const text = await res.text();
		console.log('Response text:', text);

		if (!res.ok) throw new Error(`HTTP ${res.status}: ${text}`);

		try {
			return text ? JSON.parse(text) : null;
		} catch (parseErr) {
			console.error('JSON parse error:', parseErr);
			// Se il backend ritorna testo valido, ritornalo comunque
			return text;
		}
	} catch (err) {
		console.error('Fetch error:', err);
		throw err;
	}
}