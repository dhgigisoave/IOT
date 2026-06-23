import { useState } from 'react'
import { Chart as ChartJS, ArcElement, Tooltip, Legend } from "chart.js";
import { Doughnut } from "react-chartjs-2";
import './App.css'
import { backend} from 'services/backend.js'

function App() {
    ChartJS.register(ArcElement, Tooltip, Legend);
	var data = backend.getDataFromCosmosDb()
    return (
        <>
            <Doughnut data={data} />
        </>
  )
}

export default App
