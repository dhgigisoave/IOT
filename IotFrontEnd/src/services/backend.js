import { msalInstance, loginRequest } from '../auth/msalConfig';
//const urlBase = 'https://gigi-backend-e3bff8d7cwc4cyh4.eastus-01.azurewebsites.net/api';
const urlBase = 'http://localhost:6280/api';


export async function login() {
    // Forza la schermata di login e usa popup-only
    const options = {
        ...loginRequest,
        prompt: "select_account" // o "login" se preferisci
        // NON impostare redirectUri qui se vuoi popup-only
    };

    const loginResponse = await msalInstance.loginPopup(options);
    const account = loginResponse.account;

    const tokenResponse = await msalInstance.acquireTokenSilent({
        ...loginRequest,
        account
    }).catch(async () => {
        return msalInstance.acquireTokenPopup({ ...loginRequest, account });
    });

    return { username: account.username, accessToken: tokenResponse.accessToken };
}

export async function getDataFromCosmosDb(start, end, accessToken) {
    //const auth = await login(); // consider storing token/session instead of calling login each time
    const url = `${urlBase}/misure?start=${start.toISOString()}&end=${end.toISOString()}`;

    return fetch(url, {
        headers: { Authorization: `Bearer ${accessToken}` }
    }).then(res => {
        if (!res.ok) throw new Error('Network response was not ok');
        return res.json();
    });
}

const devicesInFlight = new Map();
export async function getDevices(username, accessToken) {
    const key = `${username}|${accessToken}`;
    if (devicesInFlight.has(key)) return devicesInFlight.get(key);

    const url = `${urlBase}/devicesforuser`;

    const req = { userId: username };

    const p = fetch(url, {
        method: "POST",
        headers: {
            Authorization: `Bearer ${accessToken}`,
            "Content-Type": "application/json"
        },
        body: JSON.stringify(req)
    }).then(res => {
        if (!res.ok) throw new Error('Network response was not ok');
        return res.json();
    }).finally(() => {
        devicesInFlight.delete(key);
    });

    devicesInFlight.set(key, p);
    return p;
}
export async function claimDevice(user, accessToken, otp) {
    const url = `${urlBase}/claimdevice`;

    const req = {
        OTP: otp,
        userId: user.username
    };

    return fetch(url, {
        method: "POST",
        body: JSON.stringify(req),
        headers: { Authorization: `Bearer ${accessToken}` }
    }).then(res => {
        if (!res.ok) throw new Error('Network response was not ok');
        return res.json();
    });
}