import { useEffect, useState } from 'react';
import { InteractionStatus } from '@azure/msal-browser';
import { useMsal } from '@azure/msal-react';
import Presentation from './Presentation';
import Devices from './Devices';
import { loginRequest } from './auth/msalConfig';

function App() {
    const { instance, accounts, inProgress } = useMsal();
    const [viewPresentation, setViewPresentation] = useState(false);
    const [viewDevices, setViewDevices] = useState(false);
    const [user, setUser] = useState(null);
    const [error, setError] = useState(null);

    useEffect(() => {
        const account = instance.getActiveAccount() ?? accounts[0];
        if (account) {
            instance.setActiveAccount(account);
        }
    }, [accounts, instance]);

    useEffect(() => {
        if (inProgress !== InteractionStatus.None) return;
        if (user) return;

        const account = instance.getActiveAccount() ?? accounts[0];
        if (!account) return;

        (async () => {
            try {
                const tokenResponse = await instance.acquireTokenSilent({
                    ...loginRequest,
                    account
                });

                setUser({
                    username: account.username,
                    accessToken: tokenResponse.accessToken
                });
            } catch (e) {
                setError('Ripristino sessione fallito. ' + e.message);
                setUser({
                    username: account.username,
                    accessToken: ''
                });
            }
        })();
    }, [accounts, instance, inProgress, user]);

    async function handleLogin() {
        try {
            setError(null);
            await instance.loginRedirect({
                ...loginRequest,
                prompt: 'select_account'
            });
        } catch (e) {
            console.error(e);
            setError('Autenticazione interattiva fallita.');
        }
    }

    async function handleLogout() {
        try {
            setError(null);
            await instance.logoutPopup({
                mainWindowRedirectUri: window.location.origin
            });
        } finally {
            instance.setActiveAccount(null);
            setUser(null);
            setViewPresentation(false);
            setViewDevices(false);
        }
    }

    if (!user) {
        return (
            <div>
                <p>Utente non autenticato.</p>
                {error && <p style={{ color: 'darkred' }}>{error}</p>}
                <button onClick={handleLogin}>Accedi (redirect)</button>
            </div>
        );
    }

    return (
        <div>
            <p>Welcome, {user.username}</p>

            <button onClick={() => { setViewPresentation(true); setViewDevices(false); }}>
                Vai a Presentation
            </button>

            <button onClick={() => { setViewDevices(true); setViewPresentation(false); }}>
                Vai a Devices
            </button>

            <button onClick={handleLogout}>Esci</button>

            {viewPresentation && <Presentation />}
            {viewDevices && <Devices user={user} />}
        </div>
    );
}

export default App;