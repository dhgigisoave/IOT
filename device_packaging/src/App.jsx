import { useState } from 'react'
import * as backend from './services/backend.js'
import QRCode from 'react-qr-code'

export default function App() {
    const [seriale, setSeriale] = useState("");
    const [claimToken, setClaimToken] = useState("");

    async function inviaSeriale() {
        const response = await backend.setNewDevice(seriale);
        setClaimToken(response.otp);
    }

    return (
        <div style={{ padding: 20, fontFamily: 'sans-serif' }}>
            <h1>Production Packaging</h1>
            <p>App per la gestione del confezionamento device (uso interno).</p>

            <label>
                Inserire il seriale:
                <input
                    type="text"
                    value={seriale}
                    onChange={e => setSeriale(e.target.value)}
                    style={{ marginLeft: 8 }}
                />
            </label>

            <button onClick={inviaSeriale} style={{ marginLeft: 8 }}>invia</button>

            <div style={{ marginTop: 16 }}>
                <label style={{ display: 'block', fontWeight: 600 }}>ClaimToken:</label>
                <div>{claimToken}</div>
            </div>

            {claimToken && (
                <div style={{ marginTop: 16, background: '#fff', padding: 16, display: 'inline-block' }}>
                    <label style={{ display: 'block', marginBottom: 8 }}>OTP QR Code:</label>
                    <QRCode value={claimToken} />
                </div>
            )}
        </div>
    )
}