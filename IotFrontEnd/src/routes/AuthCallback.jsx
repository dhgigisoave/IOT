import { useEffect } from 'react';
import { msalInstance } from '../auth/msalConfig';

export default function AuthCallback() {
    useEffect(() => {
        (async () => {
            try {
                console.log('AuthCallback: handleRedirectPromise result', msalInstance);
                const resp = await msalInstance.handleRedirectPromise();
                console.log('AuthCallback: handleRedirectPromise result', resp);
            } catch (e) {
                console.error('AuthCallback handleRedirectPromise error', e);
                // non chiudere immediatamente: lascia la pagina aperta così puoi ispezionare errore
                if (window.opener) {
                    window.opener.postMessage({ type: 'msal:login:error', error: e?.message ?? String(e) }, window.location.origin);
                    // non chiudere; l'utente o tu potete vedere la console
                } else {
                    // mostra un messaggio chiaro all'utente
                    document.body.innerText = 'Errore autenticazione: ' + (e?.message ?? String(e));
                }
            }
        })();
    }, []);
    return null;
}