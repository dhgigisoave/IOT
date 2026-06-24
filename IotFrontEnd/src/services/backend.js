export async function getDataFromCosmosDb() {
	//const data = await fetch('http://gigi-backend-e3bff8d7cwc4cyh4.eastus-01.azurewebsites.net/api/misure')  // oppure l'URL di produzione	
	const url = 'http://localhost:7279/api/misure';
	// const url = 'http://gigi-backend-e3bff8d7cwc4cyh4.eastus-01.azurewebsites.net/api/misure';
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