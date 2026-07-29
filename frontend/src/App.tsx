import { useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Provider } from 'react-redux';
import { MsalProvider } from '@azure/msal-react';
import { PublicClientApplication } from '@azure/msal-browser';
import { ThemeProvider, createTheme, CssBaseline, Box, Typography } from '@mui/material';
import { store } from './store';
import { msalConfig } from './auth/msalConfig';
import LoginPage from './pages/LoginPage';
import ProtectedRoute from './components/ProtectedRoute';
import RoleProtectedRoute from './components/RoleProtectedRoute';
import LockedAccountsPage from './pages/admin/LockedAccountsPage';
import { useMsal } from '@azure/msal-react';
import { useDispatch } from 'react-redux';
import { ssoCallbackRequest } from './auth/authSlice';

const msalInstance = new PublicClientApplication(msalConfig);

const theme = createTheme({
  palette: {
    primary: { main: '#1976d2' },
    background: { default: '#f5f5f5' },
  },
  typography: {
    fontFamily: '"Inter", "Roboto", "Helvetica", "Arial", sans-serif',
  },
});

/** Placeholder dashboard shown after successful login. */
function DashboardPage() {
  return (
    <Box p={4}>
      <Typography variant="h4">Dashboard</Typography>
      <Typography color="text.secondary" mt={1}>
        Welcome to the Leave Management System.
      </Typography>
    </Box>
  );
}

/**
 * Handles MSAL redirect response (SSO callback).
 * After MSAL processes the redirect, we exchange the auth code with the backend.
 */
function MsalRedirectHandler() {
  const { instance } = useMsal();
  const dispatch = useDispatch();

  useEffect(() => {
    instance
      .handleRedirectPromise()
      .then((result) => {
        if (result?.code) {
          dispatch(ssoCallbackRequest(result.code));
        }
      })
      .catch((err: unknown) => {
        console.error('[MSAL] handleRedirectPromise error', err);
      });
  }, [instance, dispatch]);

  return null;
}

function AppRoutes() {
  return (
    <>
      <MsalRedirectHandler />
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/dashboard"
          element={
            <ProtectedRoute>
              <DashboardPage />
            </ProtectedRoute>
          }
        />
        {/* AUTH-UI-002: Locked accounts management — HR Admin + Super Admin only */}
        <Route
          path="/admin/users/locked"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <LockedAccountsPage />
            </RoleProtectedRoute>
          }
        />
        {/* Default redirect */}
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </>
  );
}

export default function App() {
  return (
    <MsalProvider instance={msalInstance}>
      <Provider store={store}>
        <ThemeProvider theme={theme}>
          <CssBaseline />
          <BrowserRouter>
            <AppRoutes />
          </BrowserRouter>
        </ThemeProvider>
      </Provider>
    </MsalProvider>
  );
}
