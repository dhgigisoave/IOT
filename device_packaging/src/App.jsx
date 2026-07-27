import React from 'react'
import { useState } from 'react'

export default function App() {
    const [seriale, setSeriale] = useState("");
    const [claimToken, setClaimToken] = useState("");


    function inviaSeriale() {
        setClaimToken(() => { return seriale + "TTT" });
    }


    return (
        <div style={{ padding: 20, fontFamily: 'sans-serif' }}>
            <h1>Production Packaging</h1>
            <p>App per la gestione del confezionamento device (uso interno).</p>
            <label>Inserire il seriale: <input type="text" onChange={e => setSeriale(e.target.value)}/></label>
            <button onClick={() => inviaSeriale()}>invia</button>
            <div>
                <label>ClaimToken:</label><label>{claimToken}</label>
            </div>
        </div>
    )
}