export async function getDataFromCosmosDb(start, end) {
	
	//const url = `http://localhost:7279/api/misure?start=${start.toISOString()}&end=${end.toISOString()}`;
	const url = `https://gigi-backend-e3bff8d7cwc4cyh4.eastus-01.azurewebsites.net/api/misure?start=${start.toISOString()}&end=${end.toISOString()}`;
	console.log(url);
	return await fetch(url)
		.then(res => {
			if (!res.ok) throw new Error('Network response was not ok');
			let ret = null;
			try {
				ret = res.json();
			} catch (e) {
				console.error("Error parsing JSON:", e);
			}
			return ret;
		});
		
}