import { useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Provider } from 'react-redux';
import { MsalProvider } from '@azure/msal-react';
import { PublicClientApplication } from '@azure/msal-browser';
import { ThemeProvider, createTheme, CssBaseline } from '@mui/material';
import { store } from './store';
import { msalConfig } from './auth/msalConfig';
import LoginPage from './pages/LoginPage';
import DashboardPage from './pages/DashboardPage';
import ProtectedRoute from './components/ProtectedRoute';
import RoleProtectedRoute from './components/RoleProtectedRoute';
import LockedAccountsPage from './pages/admin/LockedAccountsPage';
import EmployeesPage from './pages/admin/EmployeesPage';
import AuditTrailPage from './pages/admin/AuditTrailPage';
import ProfilePage from './pages/profile/ProfilePage';
import TeamPage from './pages/employees/TeamPage';
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
        {/* AUTH-UI-002: Locked accounts management */}
        <Route
          path="/admin/users/locked"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <LockedAccountsPage />
            </RoleProtectedRoute>
          }
        />
        {/* PEOPLE-UI-002: Employee management — HR Admin + Super Admin only */}
        <Route
          path="/admin/employees"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <EmployeesPage />
            </RoleProtectedRoute>
          }
        />
        {/* REPORTING-UI-005: Audit trail — HR Admin + Super Admin only */}
        <Route
          path="/admin/audit"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <AuditTrailPage />
            </RoleProtectedRoute>
          }
        />
        {/* PEOPLE-UI-002: Own profile — all authenticated users */}
        <Route
          path="/profile"
          element={
            <ProtectedRoute>
              <ProfilePage />
            </ProtectedRoute>
          }
        />
        {/* PEOPLE-UI-002: Manager team view — Manager role only */}
        <Route
          path="/employees/team"
          element={
            <RoleProtectedRoute allowedRoles={['Manager']}>
              <TeamPage />
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
