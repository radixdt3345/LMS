/**
 * REPORTING-UI-001 / Issue #57 — Employee Dashboard
 *
 * Shows:
 * - Leave balance cards (one per type) with a LinearProgress bar and
 *   a Chip showing remaining days.
 * - Doughnut chart breaking down the total allocation per leave type.
 * - Recent leave requests table (StartDate, EndDate, LeaveType, Status).
 * - "Apply for Leave" quick-action button.
 */
import { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Grid,
  LinearProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import {
  Chart as ChartJS,
  ArcElement,
  Tooltip,
  Legend,
} from 'chart.js';
import { Doughnut } from 'react-chartjs-2';
import { fetchEmployeeDashboard } from '../../store/slices/dashboardSlice';
import type { RootState, AppDispatch } from '../../store';

ChartJS.register(ArcElement, Tooltip, Legend);

const CHART_PALETTE = [
  '#1976d2', '#388e3c', '#f57c00', '#d32f2f',
  '#7b1fa2', '#0288d1', '#00796b', '#fbc02d',
];

const STATUS_COLOUR_MAP: Record<
  string,
  'success' | 'warning' | 'error' | 'default' | 'info'
> = {
  Approved: 'success',
  Pending: 'warning',
  Rejected: 'error',
  Cancelled: 'default',
  Draft: 'info',
  Revoked: 'default',
};

export default function EmployeeDashboardPage() {
  const dispatch = useDispatch<AppDispatch>();
  const navigate = useNavigate();
  const { data, loading, error } = useSelector(
    (state: RootState) => state.dashboard.employee,
  );

  useEffect(() => {
    dispatch(fetchEmployeeDashboard());
  }, [dispatch]);

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight={300}>
        <CircularProgress />
      </Box>
    );
  }

  const balances = data?.balances ?? [];
  const recentRequests = data?.recentRequests ?? [];

  const doughnutData = {
    labels: balances.map(b => b.leaveTypeName),
    datasets: [
      {
        data: balances.map(b => b.totalDays),
        backgroundColor: CHART_PALETTE.slice(0, balances.length),
        borderWidth: 1,
      },
    ],
  };

  return (
    <Box p={4}>
      {/* Header */}
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
        <Typography variant="h4">My Dashboard</Typography>
        <Button variant="contained" onClick={() => navigate('/leaves/new')}>
          Apply for Leave
        </Button>
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Grid container spacing={3}>
        {/* Leave balance cards */}
        {balances.map((balance, idx) => (
          <Grid item xs={12} sm={6} md={4} key={balance.leaveTypeName}>
            <Card variant="outlined">
              <CardContent>
                <Box
                  display="flex"
                  justifyContent="space-between"
                  alignItems="center"
                  mb={1}
                >
                  <Typography variant="subtitle1" fontWeight={600}>
                    {balance.leaveTypeName}
                  </Typography>
                  <Chip
                    label={`${balance.remainingDays} remaining`}
                    color={balance.remainingDays > 0 ? 'success' : 'default'}
                    size="small"
                  />
                </Box>
                <LinearProgress
                  variant="determinate"
                  value={
                    balance.totalDays > 0
                      ? Math.min(
                          (balance.usedDays / balance.totalDays) * 100,
                          100,
                        )
                      : 0
                  }
                  sx={{
                    height: 8,
                    borderRadius: 4,
                    backgroundColor:
                      CHART_PALETTE[idx % CHART_PALETTE.length] + '33',
                    '& .MuiLinearProgress-bar': {
                      backgroundColor: CHART_PALETTE[idx % CHART_PALETTE.length],
                    },
                  }}
                />
                <Box display="flex" justifyContent="space-between" mt={0.5}>
                  <Typography variant="caption" color="text.secondary">
                    {balance.usedDays} used
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {balance.totalDays} total
                  </Typography>
                </Box>
              </CardContent>
            </Card>
          </Grid>
        ))}

        {/* Doughnut chart — only when there is data */}
        {balances.length > 0 && (
          <Grid item xs={12} md={5}>
            <Card variant="outlined">
              <CardContent>
                <Typography variant="h6" mb={2}>
                  Leave Allocation Breakdown
                </Typography>
                <Box maxWidth={300} mx="auto">
                  <Doughnut
                    data={doughnutData}
                    options={{ plugins: { legend: { position: 'bottom' } } }}
                  />
                </Box>
              </CardContent>
            </Card>
          </Grid>
        )}

        {/* Recent requests table */}
        <Grid item xs={12}>
          <Card variant="outlined">
            <CardContent>
              <Typography variant="h6" mb={2}>
                Recent Leave Requests
              </Typography>
              {recentRequests.length === 0 ? (
                <Typography color="text.secondary">
                  No recent leave requests.
                </Typography>
              ) : (
                <TableContainer component={Paper} elevation={0}>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Leave Type</TableCell>
                        <TableCell>Start Date</TableCell>
                        <TableCell>End Date</TableCell>
                        <TableCell>Status</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {recentRequests.map(req => (
                        <TableRow key={req.requestId}>
                          <TableCell>{req.leaveTypeName}</TableCell>
                          <TableCell>{req.startDate}</TableCell>
                          <TableCell>{req.endDate}</TableCell>
                          <TableCell>
                            <Chip
                              label={req.status}
                              color={STATUS_COLOUR_MAP[req.status] ?? 'default'}
                              size="small"
                            />
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
}
