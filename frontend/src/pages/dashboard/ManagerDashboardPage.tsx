/**
 * REPORTING-UI-002 / Issue #58 — Manager Dashboard
 *
 * Shows:
 * - Pending approvals count card (click → /approvals).
 * - Bar chart of team leave utilization (dept vs total days).
 * - Team recent requests table.
 */
import { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import {
  Alert,
  Badge,
  Box,
  Button,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  CircularProgress,
  Grid,
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
  CategoryScale,
  LinearScale,
  BarElement,
  Tooltip,
  Legend,
} from 'chart.js';
import { Bar } from 'react-chartjs-2';
import { fetchManagerDashboard } from '../../store/slices/dashboardSlice';
import type { RootState, AppDispatch } from '../../store';

ChartJS.register(CategoryScale, LinearScale, BarElement, Tooltip, Legend);

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

export default function ManagerDashboardPage() {
  const dispatch = useDispatch<AppDispatch>();
  const navigate = useNavigate();
  const { data, loading, error } = useSelector(
    (state: RootState) => state.dashboard.manager,
  );

  useEffect(() => {
    dispatch(fetchManagerDashboard());
  }, [dispatch]);

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight={300}>
        <CircularProgress />
      </Box>
    );
  }

  const utilization = data?.teamUtilization ?? [];
  const teamRequests = data?.teamRecentRequests ?? [];

  const barData = {
    labels: utilization.map(r => r.departmentName),
    datasets: [
      {
        label: 'Total Leave Days',
        data: utilization.map(r => r.totalLeaveDays),
        backgroundColor: '#1976d2',
        borderRadius: 4,
      },
    ],
  };

  return (
    <Box p={4}>
      <Typography variant="h4" mb={3}>
        Manager Dashboard
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Grid container spacing={3}>
        {/* Pending approvals card */}
        <Grid item xs={12} sm={6} md={3}>
          <Card variant="outlined">
            <CardActionArea onClick={() => navigate('/approvals')}>
              <CardContent sx={{ textAlign: 'center', py: 3 }}>
                <Badge
                  badgeContent={data?.pendingApprovals ?? 0}
                  color="error"
                  max={99}
                  showZero
                >
                  <Typography variant="h2" fontWeight={700} color="primary" lineHeight={1}>
                    {data?.pendingApprovals ?? 0}
                  </Typography>
                </Badge>
                <Typography variant="subtitle1" mt={1} color="text.secondary">
                  Pending Approvals
                </Typography>
                <Button
                  size="small"
                  variant="outlined"
                  sx={{ mt: 1.5 }}
                  onClick={e => {
                    e.stopPropagation();
                    navigate('/approvals');
                  }}
                >
                  Review All
                </Button>
              </CardContent>
            </CardActionArea>
          </Card>
        </Grid>

        {/* Team utilization bar chart */}
        <Grid item xs={12} md={9}>
          <Card variant="outlined">
            <CardContent>
              <Typography variant="h6" mb={2}>
                Team Leave Utilization
              </Typography>
              {utilization.length === 0 ? (
                <Typography color="text.secondary">
                  No utilization data available.
                </Typography>
              ) : (
                <Bar
                  data={barData}
                  options={{
                    responsive: true,
                    plugins: { legend: { display: false } },
                    scales: { y: { beginAtZero: true, title: { display: true, text: 'Days' } } },
                  }}
                />
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Team recent requests table */}
        <Grid item xs={12}>
          <Card variant="outlined">
            <CardContent>
              <Typography variant="h6" mb={2}>
                Team Recent Requests
              </Typography>
              {teamRequests.length === 0 ? (
                <Typography color="text.secondary">
                  No recent requests from your team.
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
                      {teamRequests.map(req => (
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
