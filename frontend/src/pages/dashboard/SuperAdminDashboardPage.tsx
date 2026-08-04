/**
 * REPORTING-UI-004 / Issue #60 — Super Admin Dashboard
 *
 * Shows four stat cards:
 *   - Total Employees
 *   - Total Departments
 *   - Locked Accounts (orange/red when > 0, with link to /admin/users/locked)
 *   - Recent Audit Events (with link to /admin/audit)
 *
 * A note about Hangfire is shown at the bottom.
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
import LockIcon from '@mui/icons-material/Lock';
import PeopleIcon from '@mui/icons-material/People';
import BusinessIcon from '@mui/icons-material/Business';
import HistoryIcon from '@mui/icons-material/History';
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
      sx={{
        borderColor: warning ? 'warning.main' : undefined,
        height: '100%',
      }}
    >
      {onClick ? (
        <CardActionArea onClick={onClick} sx={{ height: '100%' }}>
          {content}
        </CardActionArea>
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

  const lockedCount = data?.lockedAccountCount ?? 0;

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
        <Grid item xs={12} sm={6} md={3}>
          <StatCard
            label="Total Employees"
            value={data?.totalEmployees ?? '—'}
            icon={<PeopleIcon fontSize="large" />}
          />
        </Grid>

        {/* Total Departments */}
        <Grid item xs={12} sm={6} md={3}>
          <StatCard
            label="Total Departments"
            value={data?.totalDepartments ?? '—'}
            icon={<BusinessIcon fontSize="large" />}
            colour="text.primary"
          />
        </Grid>

        {/* Locked Accounts — warning when count > 0 */}
        <Grid item xs={12} sm={6} md={3}>
          <StatCard
            label="Locked Accounts"
            value={lockedCount}
            icon={<LockIcon fontSize="large" />}
            warning={lockedCount > 0}
            onClick={() => navigate('/admin/users/locked')}
          />
        </Grid>

        {/* Recent Audit Events */}
        <Grid item xs={12} sm={6} md={3}>
          <StatCard
            label="Recent Audit Events"
            value={data?.recentAuditEventCount ?? '—'}
            icon={<HistoryIcon fontSize="large" />}
            colour="text.secondary"
            onClick={() => navigate('/admin/audit')}
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
