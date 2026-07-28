import React from 'react'
import ReactDOM from 'react-dom'
import { Provider } from 'react-redux'
import { BrowserRouter } from 'react-router-dom'
import { MsalProvider } from '@azure/msal-react'
import { PublicClientApplication } from '@azure/msal-browser'
import App from './App'
import { store } from './store'

// MSAL instance — Azure AD PKCE flow (SSO)
// cacheLocation: 'memory' so MSAL's own OAuth state is not persisted
// Our backend JWTs never touch MSAL cache — they live in Redux only
const msalInstance = new PublicClientApplication({
  auth: {
    clientId: import.meta.env.VITE_AZURE_CLIENT_ID as string,
    authority: `https://login.microsoftonline.com/${
      import.meta.env.VITE_AZURE_TENANT_ID as string
    }`,
    redirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: 'memory',
    storeAuthStateInCookie: false,
  },
})

ReactDOM.render(
  <React.StrictMode>
    <Provider store={store}>
      <MsalProvider instance={msalInstance}>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </MsalProvider>
    </Provider>
  </React.StrictMode>,
  document.getElementById('root')
)
