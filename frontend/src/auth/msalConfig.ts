import { Configuration, BrowserCacheLocation } from '@azure/msal-browser';

/**
 * MSAL configuration — reads all values from Vite env vars.
 * CONSTITUTION Rule 6: tokens stay in memory only (BrowserCacheLocation.Memory).
 * No per-user OAuth2 — this drives the SSO redirect flow only; the LMS JWT
 * is issued by the backend after /api/v1/auth/sso/callback exchange.
 */
export const msalConfig: Configuration = {
  auth: {
    clientId: import.meta.env.VITE_AZURE_CLIENT_ID ?? '',
    authority: `https://login.microsoftonline.com/${import.meta.env.VITE_AZURE_TENANT_ID ?? 'common'}`,
    redirectUri: import.meta.env.VITE_REDIRECT_URI ?? window.location.origin,
    postLogoutRedirectUri: window.location.origin,
  },
  cache: {
    // Memory only — never localStorage or sessionStorage (CONSTITUTION Rule 6).
    cacheLocation: BrowserCacheLocation.Memory,
    storeAuthStateInCookie: false,
  },
};

/** Scopes requested for the authorization code (openid profile email only — no resource scopes). */
export const loginRequest = {
  scopes: ['openid', 'profile', 'email'],
};
