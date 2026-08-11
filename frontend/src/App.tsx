import { useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate, Link, useNavigate } from 'react-router-dom';
import { Provider, useSelector } from 'react-redux';
import { MsalProvider } from '@azure/msal-react';
import { PublicClientApplication } from '@azure/msal-browser';
import {
  ThemeProvider, createTheme, CssBaseline, Box, Divider, List,
  ListItem, ListItemButton, ListItemIcon, ListItemText, Typography,
  AppBar, Toolbar, Button,
} from '@mui/material';
import DashboardIcon from '@mui/icons-material/Dashboard';
import GroupIcon from '@mui/icons-material/Group';
import PeopleIcon from '@mui/icons-material/People';
import BusinessIcon from '@mui/icons-material/Business';
import BeachAccessIcon from '@mui/icons-material/BeachAccess';
import HistoryIcon from '@mui/icons-material/History';
import SwapHorizIcon from '@mui/icons-material/SwapHoriz';
import PendingActionsIcon from '@mui/icons-material/PendingActions';
import EventIcon from '@mui/icons-material/Event';
import ListAltIcon from '@mui/icons-material/ListAlt';
import LockIcon from '@mui/icons-material/Lock';
import PolicyIcon from '@mui/icons-material/Policy';
import ManageAccountsIcon from '@mui/icons-material/ManageAccounts';
import AccountCircleIcon from '@mui/icons-material/AccountCircle';
import LogoutIcon from '@mui/icons-material/Logout';

import { store } from './store';
import { msalConfig } from './auth/msalConfig';
import type { RootState } from './store';
import { logout } from './auth/authSlice';
import { useDispatch } from 'react-redux';

import LoginPage from './pages/LoginPage';
import ProtectedRoute from './components/ProtectedRoute';
import RoleProtectedRoute from './components/RoleProtectedRoute';
import EmployeeDashboardPage from './pages/dashboard/EmployeeDashboardPage';
import ManagerDashboardPage from './pages/dashboard/ManagerDashboardPage';
import HrDashboardPage from './pages/dashboard/HrDashboardPage';
import SuperAdminDashboardPage from './pages/dashboard/SuperAdminDashboardPage';
import NewLeavePage from './pages/leaves/NewLeavePage';
import LeaveHistoryPage from './pages/leaves/LeaveHistoryPage';
import ApprovalsPage from './pages/ApprovalsPage';
import CompOffPage from './pages/CompOffPage';
import ProfilePage from './pages/ProfilePage';
import TeamPage from './pages/TeamPage';
import AllLeavesPage from './pages/admin/AllLeavesPage';
import LockedAccountsPage from './pages/admin/LockedAccountsPage';
import EmployeesPage from './pages/admin/EmployeesPage';
import DepartmentsPage from './pages/admin/DepartmentsPage';
import LeaveTypesPage from './pages/admin/LeaveTypesPage';
import AuditTrailPage from './pages/admin/AuditTrailPage';
import HolidaysPage from './pages/HolidaysPage';
import NotificationBell from './components/NotificationBell';
import { useMsal } from '@azure/msal-react';
import { ssoCallbackRequest } from './auth/authSlice';

const msalInstance = new PublicClientApplication(msalConfig);

const theme = createTheme({
  palette: { primary: { main: '#1976d2' }, background: { default: '#f5f5f5' } },
  typography: { fontFamily: '"Inter", "Roboto", "Helvetica", "Arial", sans-serif' },
});

function DefaultDashboardRedirect() {
  const role = useSelector((state: RootState) => state.auth.user?.role);
  if (role === 'SuperAdmin') return <Navigate to="/dashboard/super-admin" replace />;
  if (role === 'HRAdmin') return <Navigate to="/dashboard/hr" replace />;
  if (role === 'Manager') return <Navigate to="/dashboard/manager" replace />;
  return <Navigate to="/dashboard/employee" replace />;
}

function MsalRedirectHandler() {
  const { instance } = useMsal();
  const dispatch = useDispatch();
  useEffect(() => {
    instance.handleRedirectPromise()
      .then(result => { if (result?.code) dispatch(ssoCallbackRequest(result.code)); })
      .catch((err: unknown) => console.error('[MSAL] redirect error', err));
  }, [instance, dispatch]);
  return null;
}

function AppShell({ children }: { children: React.ReactNode }) {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const email = useSelector((state: RootState) => state.auth.user?.email);
  return (
    <Box display="flex" flexDirection="column" minHeight="100vh">
      <AppBar position="static" elevation={1}>
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>Leave Management</Typography>
          {email && <Typography variant="body2" sx={{ mr: 2, opacity: 0.8 }}>{email}</Typography>}
          <NotificationBell />
          <Button color="inherit" size="small" startIcon={<LogoutIcon />}
            onClick={() => { dispatch(logout()); navigate('/login'); }} sx={{ ml: 1 }}>
            Logout
          </Button>
        </Toolbar>
      </AppBar>
      <Box display="flex" flex={1}><Box flex={1} overflow="auto">{children}</Box></Box>
    </Box>
  );
}

function AppRoutes() {
  return (
    <>
      <MsalRedirectHandler />
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<ProtectedRoute><DefaultDashboardRedirect /></ProtectedRoute>} />
        <Route path="/dashboard" element={<ProtectedRoute><DefaultDashboardRedirect /></ProtectedRoute>} />
        <Route path="/dashboard/employee" element={<RoleProtectedRoute allowedRoles={['Employee','Manager']}><AppShell><EmployeeDashboardPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/dashboard/manager" element={<RoleProtectedRoute allowedRoles={['Manager','HRAdmin','SuperAdmin']}><AppShell><ManagerDashboardPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/dashboard/hr" element={<RoleProtectedRoute allowedRoles={['HRAdmin','SuperAdmin']}><AppShell><HrDashboardPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/dashboard/super-admin" element={<RoleProtectedRoute allowedRoles={['SuperAdmin']}><AppShell><SuperAdminDashboardPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/leaves/new" element={<RoleProtectedRoute allowedRoles={['Employee','Manager']}><AppShell><NewLeavePage /></AppShell></RoleProtectedRoute>} />
        <Route path="/leaves/history" element={<RoleProtectedRoute allowedRoles={['Employee','Manager']}><AppShell><LeaveHistoryPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/comp-off" element={<RoleProtectedRoute allowedRoles={['Employee','Manager']}><AppShell><CompOffPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/approvals" element={<RoleProtectedRoute allowedRoles={['Manager','HRAdmin','SuperAdmin']}><AppShell><ApprovalsPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/team" element={<RoleProtectedRoute allowedRoles={['Manager']}><AppShell><TeamPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/profile" element={<ProtectedRoute><AppShell><ProfilePage /></AppShell></ProtectedRoute>} />
        <Route path="/holidays" element={<RoleProtectedRoute allowedRoles={['HRAdmin','SuperAdmin']}><AppShell><HolidaysPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/admin/leaves" element={<RoleProtectedRoute allowedRoles={['HRAdmin','SuperAdmin']}><AppShell><AllLeavesPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/admin/employees" element={<RoleProtectedRoute allowedRoles={['HRAdmin','SuperAdmin']}><AppShell><EmployeesPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/admin/departments" element={<RoleProtectedRoute allowedRoles={['HRAdmin','SuperAdmin']}><AppShell><DepartmentsPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/admin/leave-types" element={<RoleProtectedRoute allowedRoles={['HRAdmin','SuperAdmin']}><AppShell><LeaveTypesPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/admin/users/locked" element={<RoleProtectedRoute allowedRoles={['HRAdmin','SuperAdmin']}><AppShell><LockedAccountsPage /></AppShell></RoleProtectedRoute>} />
        <Route path="/admin/audit" element={<RoleProtectedRoute allowedRoles={['HRAdmin','SuperAdmin']}><AppShell><AuditTrailPage /></AppShell></RoleProtectedRoute>} />
        <Route path="*" element={<Navigate to="/" replace />} />
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
