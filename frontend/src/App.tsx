import { useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate, Link, useNavigate } from 'react-router-dom';
import { Provider, useSelector } from 'react-redux';
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
  ListItemIcon,
  ListItemText,
  Typography,
  AppBar,
  Toolbar,
  Button,
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

// Pages
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
  palette: {
    primary: { main: '#1976d2' },
    background: { default: '#f5f5f5' },
  },
  typography: {
    fontFamily: '"Inter", "Roboto", "Helvetica", "Arial", sans-serif',
  },
});

// ---------------------------------------------------------------------------
// Role-based sidebar navigation
// ---------------------------------------------------------------------------

interface NavItem {
  label: string;
  to: string;
  icon: React.ReactNode;
  roles?: string[]; // if omitted, visible to all authenticated users
}

interface NavSection {
  title: string;
  items: NavItem[];
  roles?: string[]; // if omitted, section visible to all
}

const NAV_CONFIG: NavSection[] = [
  {
    title: 'Dashboard',
    items: [
      { label: 'My Dashboard', to: '/dashboard/employee', icon: <DashboardIcon fontSize="small" />, roles: ['Employee', 'Manager'] },
      { label: 'My Dashboard', to: '/dashboard/employee', icon: <DashboardIcon fontSize="small" />, roles: ['HRAdmin'] },
      { label: 'My Dashboard', to: '/dashboard/super-admin', icon: <DashboardIcon fontSize="small" />, roles: ['SuperAdmin'] },
      { label: 'Manager View', to: '/dashboard/manager', icon: <GroupIcon fontSize="small" />, roles: ['Manager', 'HRAdmin', 'SuperAdmin'] },
      { label: 'HR View', to: '/dashboard/hr', icon: <ManageAccountsIcon fontSize="small" />, roles: ['HRAdmin', 'SuperAdmin'] },
    ],
  },
  {
    title: 'Leave',
    items: [
      { label: 'Apply for Leave', to: '/leaves/new', icon: <BeachAccessIcon fontSize="small" />, roles: ['Employee', 'Manager'] },
      { label: 'My History', to: '/leaves/history', icon: <HistoryIcon fontSize="small" />, roles: ['Employee', 'Manager'] },
      { label: 'Comp-Off', to: '/comp-off', icon: <SwapHorizIcon fontSize="small" />, roles: ['Employee', 'Manager'] },
      { label: 'Approvals', to: '/approvals', icon: <PendingActionsIcon fontSize="small" />, roles: ['Manager', 'HRAdmin', 'SuperAdmin'] },
      { label: 'My Team', to: '/team', icon: <GroupIcon fontSize="small" />, roles: ['Manager'] },
    ],
  },
  {
    title: 'Admin',
    roles: ['HRAdmin', 'SuperAdmin'],
    items: [
      { label: 'All Leaves', to: '/admin/leaves', icon: <ListAltIcon fontSize="small" /> },
      { label: 'Employees', to: '/admin/employees', icon: <PeopleIcon fontSize="small" /> },
      { label: 'Departments', to: '/admin/departments', icon: <BusinessIcon fontSize="small" /> },
      { label: 'Leave Types', to: '/admin/leave-types', icon: <PolicyIcon fontSize="small" /> },
      { label: 'Holidays', to: '/holidays', icon: <EventIcon fontSize="small" /> },
      { label: 'Locked Accounts', to: '/admin/users/locked', icon: <LockIcon fontSize="small" /> },
      { label: 'Audit Trail', to: '/admin/audit', icon: <HistoryIcon fontSize="small" /> },
    ],
  },
  {
    title: 'Account',
    items: [
      { label: 'My Profile', to: '/profile', icon: <AccountCircleIcon fontSize="small" /> },
    ],
  },
];

function SidebarNav() {
  const role = useSelector((state: RootState) => state.auth.user?.role) ?? '';

  return (
    <Box
      component="nav"
      sx={{
        width: 230,
        flexShrink: 0,
        borderRight: '1px solid',
        borderColor: 'divider',
        minHeight: '100%',
        pt: 1,
        bgcolor: 'background.paper',
        overflowY: 'auto',
      }}
      aria-label="main navigation"
    >
      {NAV_CONFIG.map(section => {
        // Filter section by role
        if (section.roles && !section.roles.includes(role)) return null;

        // Filter items by role — deduplicate "My Dashboard" labels
        const seen = new Set<string>();
        const visibleItems = section.items.filter(item => {
          if (item.roles && !item.roles.includes(role)) return false;
          // Deduplicate by `to` path (avoid two "My Dashboard" links for HRAdmin)
          if (seen.has(item.to)) return false;
          seen.add(item.to);
          return true;
        });

        if (visibleItems.length === 0) return null;

        return (
          <Box key={section.title}>
            <Typography variant="overline" px={2} py={0.5} color="text.secondary" display="block" fontSize="0.65rem">
              {section.title}
            </Typography>
            <List dense disablePadding>
              {visibleItems.map(item => (
                <ListItem key={`${item.to}-${item.label}`} disablePadding>
                  <ListItemButton component={Link} to={item.to} sx={{ py: 0.5 }}>
                    <ListItemIcon sx={{ minWidth: 32 }}>{item.icon}</ListItemIcon>
                    <ListItemText
                      primary={item.label}
                      primaryTypographyProps={{ variant: 'body2' }}
                    />
                  </ListItemButton>
                </ListItem>
              ))}
            </List>
            <Divider sx={{ my: 0.5 }} />
          </Box>
        );
      })}
    </Box>
  );
}

// ---------------------------------------------------------------------------
// AppShell — authenticated layout with AppBar + Sidebar
// ---------------------------------------------------------------------------

function AppShell({ children }: { children: React.ReactNode }) {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const email = useSelector((state: RootState) => state.auth.user?.email);

  const handleLogout = () => {
    dispatch(logout());
    navigate('/login');
  };

  return (
    <Box display="flex" flexDirection="column" minHeight="100vh">
      <AppBar position="static" elevation={1}>
        <Toolbar>
          <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
            Leave Management
          </Typography>
          {email && (
            <Typography variant="body2" sx={{ mr: 2, opacity: 0.8 }}>
              {email}
            </Typography>
          )}
          <NotificationBell />
          <Button
            color="inherit"
            size="small"
            startIcon={<LogoutIcon />}
            onClick={handleLogout}
            sx={{ ml: 1 }}
          >
            Logout
          </Button>
        </Toolbar>
      </AppBar>
      <Box display="flex" flex={1}>
        <SidebarNav />
        <Box flex={1} overflow="auto">{children}</Box>
      </Box>
    </Box>
  );
}

// ---------------------------------------------------------------------------
// MSAL redirect handler
// ---------------------------------------------------------------------------

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

// ---------------------------------------------------------------------------
// Role-based default dashboard redirect
// ---------------------------------------------------------------------------

function DefaultDashboardRedirect() {
  const role = useSelector((state: RootState) => state.auth.user?.role);
  if (role === 'SuperAdmin') return <Navigate to="/dashboard/super-admin" replace />;
  if (role === 'HRAdmin') return <Navigate to="/dashboard/hr" replace />;
  if (role === 'Manager') return <Navigate to="/dashboard/manager" replace />;
  return <Navigate to="/dashboard/employee" replace />;
}

// ---------------------------------------------------------------------------
// All routes
// ---------------------------------------------------------------------------

function AppRoutes() {
  return (
    <>
      <MsalRedirectHandler />
      <Routes>
        {/* Public */}
        <Route path="/login" element={<LoginPage />} />

        {/* Default redirect — role-aware */}
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <DefaultDashboardRedirect />
            </ProtectedRoute>
          }
        />
        <Route
          path="/dashboard"
          element={
            <ProtectedRoute>
              <DefaultDashboardRedirect />
            </ProtectedRoute>
          }
        />

        {/* ── Dashboard routes ── */}
        <Route
          path="/dashboard/employee"
          element={
            <RoleProtectedRoute allowedRoles={['Employee', 'Manager']}>
              <AppShell><EmployeeDashboardPage /></AppShell>
            </RoleProtectedRoute>
          }
        />
        <Route
          path="/dashboard/manager"
          element={
            <RoleProtectedRoute allowedRoles={['Manager', 'HRAdmin', 'SuperAdmin']}>
              <AppShell><ManagerDashboardPage /></AppShell>
            </RoleProtectedRoute>
          }
        />
        <Route
          path="/dashboard/hr"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <AppShell><HrDashboardPage /></AppShell>
            </RoleProtectedRoute>
          }
        />
        <Route
          path="/dashboard/super-admin"
          element={
            <RoleProtectedRoute allowedRoles={['SuperAdmin']}>
              <AppShell><SuperAdminDashboardPage /></AppShell>
            </RoleProtectedRoute>
          }
        />

        {/* ── Leave routes — Employee + Manager only ── */}
        <Route
          path="/leaves/new"
          element={
            <RoleProtectedRoute allowedRoles={['Employee', 'Manager']}>
              <AppShell><NewLeavePage /></AppShell>
            </RoleProtectedRoute>
          }
        />
        <Route
          path="/leaves/history"
          element={
            <RoleProtectedRoute allowedRoles={['Employee', 'Manager']}>
              <AppShell><LeaveHistoryPage /></AppShell>
            </RoleProtectedRoute>
          }
        />
        <Route
          path="/comp-off"
          element={
            <RoleProtectedRoute allowedRoles={['Employee', 'Manager']}>
              <AppShell><CompOffPage /></AppShell>
            </RoleProtectedRoute>
          }
        />

        {/* ── Approvals — Manager, HRAdmin, SuperAdmin ── */}
        <Route
          path="/approvals"
          element={
            <RoleProtectedRoute allowedRoles={['Manager', 'HRAdmin', 'SuperAdmin']}>
              <AppShell><ApprovalsPage /></AppShell>
            </RoleProtectedRoute>
          }
        />

        {/* ── Team — Manager only ── */}
        <Route
          path="/team"
          element={
            <RoleProtectedRoute allowedRoles={['Manager']}>
              <AppShell><TeamPage /></AppShell>
            </RoleProtectedRoute>
          }
        />

        {/* ── Profile — all authenticated ── */}
        <Route
          path="/profile"
          element={
            <ProtectedRoute>
              <AppShell><ProfilePage /></AppShell>
            </ProtectedRoute>
          }
        />

        {/* ── Holidays — HRAdmin, SuperAdmin (manage); all auth can view ── */}
        <Route
          path="/holidays"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <AppShell><HolidaysPage /></AppShell>
            </RoleProtectedRoute>
          }
        />

        {/* ── Admin routes — HRAdmin, SuperAdmin ── */}
        <Route
          path="/admin/leaves"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <AppShell><AllLeavesPage /></AppShell>
            </RoleProtectedRoute>
          }
        />
        <Route
          path="/admin/employees"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <AppShell><EmployeesPage /></AppShell>
            </RoleProtectedRoute>
          }
        />
        <Route
          path="/admin/departments"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <AppShell><DepartmentsPage /></AppShell>
            </RoleProtectedRoute>
          }
        />
        <Route
          path="/admin/leave-types"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <AppShell><LeaveTypesPage /></AppShell>
            </RoleProtectedRoute>
          }
        />
        <Route
          path="/admin/users/locked"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <AppShell><LockedAccountsPage /></AppShell>
            </RoleProtectedRoute>
          }
        />
        <Route
          path="/admin/audit"
          element={
            <RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}>
              <AppShell><AuditTrailPage /></AppShell>
            </RoleProtectedRoute>
          }
        />

        {/* Catch-all */}
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
