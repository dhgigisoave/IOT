import { msalInstance } from './auth/msalConfig';

(async () => {
    try {
        const result = await msalInstance.handleRedirectPromise();

        if (result?.account) {
            msalInstance.setActiveAccount(result.account);

            window.opener?.postMessage(
                {
                    type: 'msal:login',
                    account: result.account.username
                },
                window.location.origin
            );
        }
    } catch (e) {
        window.opener?.postMessage(
            {
                type: 'msal:login:error',
                error: e?.message ?? String(e)
            },
            window.location.origin
        );
    } finally {
        setTimeout(() => window.close(), 100);
    }
})();