import { useEffect } from 'react';

export default function PopupClose() {
	useEffect(() => {
		window.close();
	}, []);

	return null;
}