import { useState, useEffect } from 'react';
import * as backend from './services/backend';
import "./index.css"

function ClaimDevice({ user, accessToken, setClaimDevice }) {
    const [otp, setOtp] = useState("");
    const [error, setError] = useState("");

    async function handleClaimOtp() {
        try {
			setError("");
            await backend.claimDevice(user, accessToken, otp);
            setClaimDevice(false);

		} catch (e) {
			setError('Errore durante la richiesta di claim del device: ' + e.message);
		}
    }

    return (
        <>
            <label>OTP:</label>
            <input
                type="text"
                value={otp}
                onChange={e => setOtp(e.target.value)}
            />
            <button onClick={() => handleClaimOtp()}>Claim</button>
			{error && <p style={{ color: 'red' }}>{error}</p>}
        </>
    );
}
function Devices({ user }) {
    const [devices, setDevices] = useState([]);
    const [claimDevice, setClaimDevice] = useState(false);

    useEffect(() => {
        // Fetch devices for the user
        async function fetchDevices() {
            try {
                const response = await backend.getDevices(user.username, user.accessToken);
                setDevices(response);
            } catch (error) {
                console.error('Error fetching devices:', error);
            }
        }
        fetchDevices();
    }, [user]);



    return (
        <>
            <p>Devices for {user.username}</p>
			{devices.length === 0 ? (
                <p>No devices found.</p>   
            ) : (
                    <table className="devices-table">
                        <thead>
                            <tr>
                                <th>ID</th>
                                <th>Name</th>
                                <th>Description</th>
                                <th>Location</th>
                                <th>Created</th>
                            </tr>
                        </thead>
                        <tbody>
                            {devices.map((device) => (
                                <tr key={device.id}>
                                    <td>{device.id}</td>
                                    <td>{device.name}</td>
                                    <td>{device.description || '-'}</td>
                                    <td>{device.location || '-'}</td>
                                    <td>{new Date(device.createdAt).toLocaleString()}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
            )}
            <button onClick={() => { setClaimDevice(true) }}>Claim Device</button>
            {claimDevice && <ClaimDevice user={user} accessToken={user.accessToken} setClaimDevice={setClaimDevice} />}
        </>
  );
}

export default Devices;