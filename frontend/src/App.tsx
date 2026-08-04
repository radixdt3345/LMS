import { useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate, Link } from 'react-router-dom';
import { Provider } from 'react-redux';
import { MsalProvider } from '@azure/msal-react';
import { PublicClientApplication } from '@azure/msal-browser';
import {
  ThemeProvider,
  createTheme,
  CssBaseline,
  Box,
  Divider,
  List,
  ListItem,
  ListItemButton,
  ListItemText,
  Typography,
  AppBar,
  Toolbar,
} from '@mui/material';
import { store } from './store';
import { msalConfig } from './auth/msalConfig';
import LoginPage from './pages/LoginPage';
import ProtectedRoute from './components/ProtectedRoute';
import RoleProtectedRoute from './components/RoleProtectedRoute';
import LockedAccountsPage from './pages/admin/LockedAccountsPage';
import NewLeavePage from './pages/leaves/NewLeavePage';
import LeaveHistoryPage from './pages/leaves/LeaveHistoryPage';
import AllLeavesPage from './pages/admin/AllLeavesPage';
import NotificationBell from './components/NotificationBell';
import { useMsal } from '@azure/msal-react';
import { useDispatch } from 'react-redux';
import { ssoCallbackRequest } from './auth/authSlice';
import EmployeeDashboardPage from './pages/dashboard/EmployeeDashboardPage';
import ManagerDashboardPage from './pages/dashboard/ManagerDashboardPage';
import HrDashboardPage from './pages/dashboard/HrDashboardPage';
import SuperAdminDashboardPage from './pages/dashboard/SuperAdminDashboardPage';

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
 * Sidebar navigation — lightweight list of links for authenticated users.
 */
function SidebarNav() {
  return (
    <Box
      component="nav"
      sx={{
        width: 220,
        flexShrink: 0,
        borderRight: '1px solid',
        borderColor: 'divider',
        minHeight: '100%',
        pt: 2,
        bgcolor: 'background.paper',
      }}
      aria-label="main navigation"
    >
      <Typography variant="subtitle2" px={2} py={1} color="text.secondary">
        Dashboard
      </Typography>
      <List dense disablePadding>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/dashboard">
            <ListItemText primary="My Dashboard" />
          </ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/dashboard/manager">
            <ListItemText primary="Manager View" />
          </ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/dashboard/hr">
            <ListItemText primary="HR View" />
          </ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/dashboard/super-admin">
            <ListItemText primary="Admin View" />
          </ListItemButton>
        </ListItem>
      </List>
      <Divider sx={{ my: 1 }} />
      <Typography variant="subtitle2" px={2} py={1} color="text.secondary">
        Leave
      </Typography>
      <List dense disablePadding>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/leaves/new">
            <ListItemText primary="New Request" />
          </ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/leaves/history">
            <ListItemText primary="My History" />
          </ListItemButton>
        </ListItem>
      </List>
      <Divider sx={{ my: 1 }} />
      <Typography variant="subtitle2" px={2} py={1} color="text.secondary">
        Admin
      </Typography>
      <List dense disablePadding>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/admin/leaves">
            <ListItemText primary="All Leaves" />
          </ListItemButton>
        </ListItem>
        <ListItem disablePadding>
          <ListItemButton component={Link} to="/admin/users/locked">
            <ListItemText primary="Locked Accounts" />
          </ListItemButton>
        </ListItem>
      </List>
    </Box>
  );
}

/**
 * AppShell — wraps all authenticated pages with a top AppBar (containing the
 * NotificationBell) and the sidebar navigation.
 */
function AppShell({ children }: { children: React.ReactNode }) {
  return (
    <Box display="flex" flexDirection="column" minHeight="100vh">
      <AppBar position="static" elevation={1}>
        <Toolbar>
          <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
            Leave Management
          </Typography>
          <NotificationBell />
        </Toolbar>
      </AppBar>
      <Box display="flex" flex={1}>
        <SidebarNav />
        <Box flex={1}>{children}</Box>
      </Box>
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
        {/* Public */}
        <Route path="/login" element={<LoginPage />} />

        {/* ------------------------------------------------------------------ */}
        {/* Dashboard routes — #57 #58 #59 #60                                  */}
        {/* ------------------------------------------------------------------ */}

        {/* Employee dashboard — all authenticated users */}
        <Route
          path="/dashboard"
          element={
            <ProtectedRoute>
              <AppShell>
                <EmployeeDashboardPage />
              </AppShell>
            </ProtectedRoute>
          }
        />
        <Route
          path="/dashboard/employee"
          element={
            <ProtectedRoute>
              <AppShell>
                <EmployeeDashboardPage />
              </AppShell>
            </ProtectedRoute>
          }
        />

        {/* Manager dashboard — Manager, HRAdmin, SuperAdmin */}
        <Route
          path="/dashboard/manager"
          element={
            <RoleProtectedRoute allowedRoles={['Manager', 'HRAdmin', 'SuperAdmin']}>
              <AppShell>
                <ManagerDashboardPage />
              </AppShell>
            </RoleProtectedRoute>
          }
        />

        {/* HR Admin dashboard — HRAdmin, SuperAdmin */}
        <Route
          path="/dashboard/hr"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <AppShell>
                <HrDashboardPage />
              </AppShell>
            </RoleProtectedRoute>
          }
        />

        {/* Super Admin dashboard — SuperAdmin only */}
        <Route
          path="/dashboard/super-admin"
          element={
            <RoleProtectedRoute allowedRoles={['SuperAdmin']}>
              <AppShell>
                <SuperAdminDashboardPage />
              </AppShell>
            </RoleProtectedRoute>
          }
        />

        {/* ------------------------------------------------------------------ */}
        {/* LEAVECORE-UI-004: Leave request form                                */}
        {/* ------------------------------------------------------------------ */}
        <Route
          path="/leaves/new"
          element={
            <ProtectedRoute>
              <AppShell>
                <NewLeavePage />
              </AppShell>
            </ProtectedRoute>
          }
        />

        {/* LEAVECORE-UI-004: Leave history */}
        <Route
          path="/leaves/history"
          element={
            <ProtectedRoute>
              <AppShell>
                <LeaveHistoryPage />
              </AppShell>
            </ProtectedRoute>
          }
        />

        {/* LEAVECORE-UI-004: All leaves — HR Admin + Super Admin only */}
        <Route
          path="/admin/leaves"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <AppShell>
                <AllLeavesPage />
              </AppShell>
            </RoleProtectedRoute>
          }
        />

        {/* AUTH-UI-002: Locked accounts management — HR Admin + Super Admin only */}
        <Route
          path="/admin/users/locked"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <AppShell>
                <LockedAccountsPage />
              </AppShell>
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
