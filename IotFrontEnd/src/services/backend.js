function getDataFromCosmosDb() {
	var data = fetch('http://gigi-backend-e3bff8d7cwc4cyh4.eastus-01.azurewebsites.net')  // oppure l'URL di produzione
		.then(response => response.json())
		.then(data => console.log(data));
	return data;
		
}