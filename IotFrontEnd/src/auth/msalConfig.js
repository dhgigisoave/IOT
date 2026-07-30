import { PublicClientApplication } from "@azure/msal-browser";

export const msalConfig = {
    auth: {
        clientId: "69d86177-b2b6-427d-aaf0-adca2d188aea", // replace
        authority: "https://login.microsoftonline.com/46ce2e07-99e1-43cc-b7d7-17bf92113dc0", // replace
        redirectUri: window.location.origin
        //redirectUri: window.location.origin + "/auth/callback"
        //redirectUri: window.location.origin + "/?msalPopup=1"
        //redirectUri: window.location.origin + "/auth-popup.html"
    },
    cache: {
        cacheLocation: "localStorage",
        storeAuthStateInCookie: false
    }
};

export const loginRequest = {
    scopes: ["openid", "profile", "api://14f20a85-5b11-4d49-af02-e9bd1b64a489/access_as_user"] // e.g. "api://{backend-client-id}/access_as_user"
};

export const msalInstance = new PublicClientApplication(msalConfig);