/**
 * REPORTING-UI-004 / Issue #60 — Super Admin Dashboard
 *
 * Shows five stat cards:
 *   - Total Employees
 *   - Total Departments
 *   - Active Leave Today
 *   - Pending Approvals (orange when > 0)
 *   - System Leave Utilization %
 *
 * Plus a note about Hangfire.
 */
import { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import {
  Alert,
  Box,
  Card,
  CardActionArea,
  CardContent,
  CircularProgress,
  Grid,
  Typography,
} from '@mui/material';
import PeopleIcon from '@mui/icons-material/People';
import BusinessIcon from '@mui/icons-material/Business';
import EventBusyIcon from '@mui/icons-material/EventBusy';
import PendingActionsIcon from '@mui/icons-material/PendingActions';
import BarChartIcon from '@mui/icons-material/BarChart';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import { fetchSuperAdminDashboard } from '../../store/slices/dashboardSlice';
import type { RootState, AppDispatch } from '../../store';

interface StatCardProps {
  label: string;
  value: number | string;
  icon: React.ReactNode;
  colour?: string;
  onClick?: () => void;
  warning?: boolean;
}

function StatCard({ label, value, icon, colour, onClick, warning }: StatCardProps) {
  const content = (
    <CardContent sx={{ textAlign: 'center', py: 3 }}>
      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        mb={1}
        color={warning ? 'warning.main' : colour ?? 'primary.main'}
      >
        {icon}
      </Box>
      <Typography
        variant="h3"
        fontWeight={700}
        color={warning ? 'warning.main' : colour ?? 'primary.main'}
      >
        {value}
      </Typography>
      <Typography variant="subtitle2" color="text.secondary" mt={0.5}>
        {label}
      </Typography>
      {warning && (
        <Box display="flex" justifyContent="center" alignItems="center" gap={0.5} mt={1}>
          <WarningAmberIcon fontSize="small" color="warning" />
          <Typography variant="caption" color="warning.main">
            Action required
          </Typography>
        </Box>
      )}
    </CardContent>
  );

  return (
    <Card
      variant="outlined"
      sx={{ borderColor: warning ? 'warning.main' : undefined, height: '100%' }}
    >
      {onClick ? (
        <CardActionArea onClick={onClick} sx={{ height: '100%' }}>{content}</CardActionArea>
      ) : (
        content
      )}
    </Card>
  );
}

export default function SuperAdminDashboardPage() {
  const dispatch = useDispatch<AppDispatch>();
  const navigate = useNavigate();
  const { data, loading, error } = useSelector(
    (state: RootState) => state.dashboard.superAdmin,
  );

  useEffect(() => {
    dispatch(fetchSuperAdminDashboard());
  }, [dispatch]);

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight={300}>
        <CircularProgress />
      </Box>
    );
  }

  const pendingApprovals = data?.pendingApprovals ?? 0;
  const utilPct = data?.systemLeaveUtilizationPercent ?? 0;

  return (
    <Box p={4}>
      <Typography variant="h4" mb={3}>
        Super Admin Dashboard
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Grid container spacing={3} mb={4}>
        {/* Total Employees */}
        <Grid item xs={12} sm={6} md={4} lg={2}>
          <StatCard
            label="Total Employees"
            value={data?.totalEmployees ?? '—'}
            icon={<PeopleIcon fontSize="large" />}
          />
        </Grid>

        {/* Total Departments */}
        <Grid item xs={12} sm={6} md={4} lg={2}>
          <StatCard
            label="Total Departments"
            value={data?.totalDepartments ?? '—'}
            icon={<BusinessIcon fontSize="large" />}
            colour="text.primary"
          />
        </Grid>

        {/* Active Leave Today */}
        <Grid item xs={12} sm={6} md={4} lg={2}>
          <StatCard
            label="On Leave Today"
            value={data?.activeLeaveToday ?? '—'}
            icon={<EventBusyIcon fontSize="large" />}
            colour="info.main"
          />
        </Grid>

        {/* Pending Approvals — warning when > 0 */}
        <Grid item xs={12} sm={6} md={4} lg={3}>
          <StatCard
            label="Pending Approvals"
            value={pendingApprovals}
            icon={<PendingActionsIcon fontSize="large" />}
            warning={pendingApprovals > 0}
            onClick={() => navigate('/approvals')}
          />
        </Grid>

        {/* System Leave Utilization % */}
        <Grid item xs={12} sm={6} md={4} lg={3}>
          <StatCard
            label="System Utilization"
            value={`${utilPct.toFixed(1)}%`}
            icon={<BarChartIcon fontSize="large" />}
            colour="success.main"
          />
        </Grid>
      </Grid>

      {/* Hangfire note */}
      <Box
        sx={{
          p: 2,
          borderRadius: 1,
          bgcolor: 'grey.100',
          border: '1px solid',
          borderColor: 'grey.300',
        }}
      >
        <Typography variant="body2" color="text.secondary">
          Background jobs managed via Hangfire — check /hangfire for scheduler
          status. Hangfire UI runs on a separate port and is not linked here.
        </Typography>
      </Box>
    </Box>
  );
}
