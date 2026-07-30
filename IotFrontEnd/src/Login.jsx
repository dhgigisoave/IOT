import { useState } from 'react';
import { msalInstance, loginRequest } from './auth/msalConfig';

function Login({ setUser }) {
	const [username, setUsername] = useState("");
	const [password, setPassword] = useState("");

	async function  handleLogin() {
		// Call loginPopup directly in the click handler so popup.open runs in the user gesture stack
		const loginResponse = await msalInstance.loginPopup(loginRequest);
		const account = loginResponse.account;

		const tokenResponse = await msalInstance.acquireTokenSilent({
			...loginRequest,
			account
		}).catch(async () => {
			return msalInstance.acquireTokenPopup({ ...loginRequest, account });
		});

		setUser({ username: account.username, accessToken: tokenResponse.accessToken });
	}	

	return (
		<div style={{ padding: 20, fontFamily: 'sans-serif' }}>
			<h1>Login</h1>
			<label>
				Username:
				<input
					type="text"
					value={username}
					onChange={e => setUsername(e.target.value)}
					style={{ marginLeft: 8 }}
				/>
			</label>
			<br />
			<label>
				Password:
				<input
					type="password"
					value={password}
					onChange={e => setPassword(e.target.value)}
					style={{ marginLeft: 8 }}
				/>
			</label>
			<button
				disabled={!username || !password}
				onClick={() => {
					handleLogin();
			}}>Login</button>
		</div>
	);
}

export default Login;