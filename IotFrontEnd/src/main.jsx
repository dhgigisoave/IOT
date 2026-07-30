import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { MsalProvider } from '@azure/msal-react';
import { msalInstance } from './auth/msalConfig';
import './index.css';
import App from './App.jsx';
import PopupClose from './PopupClose'
import {
	Chart as ChartJS,
	CategoryScale,
	LinearScale,
	PointElement,
	LineElement,
	ArcElement,
	Tooltip,
	Legend
} from "chart.js";

ChartJS.register(
	CategoryScale,
	LinearScale,
	PointElement,
	LineElement,
	ArcElement,
	Tooltip,
	Legend
);


msalInstance.initialize().then(() => {
	const root = createRoot(document.getElementById('root'));
	const isPopup = new URL(window.location.href).searchParams.get('msalPopup') === '1';
	if (isPopup) {
		root.render(
			<StrictMode>
				<PopupClose />
			</StrictMode>
		);
	} else {
		root.render(
			<StrictMode>
				<MsalProvider instance={msalInstance}>
					<App />
				</MsalProvider>
			</StrictMode>
		);
	}
});