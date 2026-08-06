/**
 * REPORTING-UI-002 / Issue #58 — Manager Dashboard
 *
 * Shows:
 * - Team size stat card.
 * - Pending requests count card (click → /approvals).
 * - Table of team pending leave requests.
 */
import { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import {
  Alert,
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
import GroupIcon from '@mui/icons-material/Group';
import PendingActionsIcon from '@mui/icons-material/PendingActions';
import { fetchManagerDashboard } from '../../store/slices/dashboardSlice';
import type { RootState, AppDispatch } from '../../store';

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

  const teamPendingRequests = data?.teamPendingRequests ?? [];
  const pendingCount = teamPendingRequests.length;

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
        {/* Team size card */}
        <Grid item xs={12} sm={6} md={3}>
          <Card variant="outlined">
            <CardContent sx={{ textAlign: 'center', py: 3 }}>
              <Box display="flex" justifyContent="center" mb={1} color="primary.main">
                <GroupIcon fontSize="large" />
              </Box>
              <Typography variant="h3" fontWeight={700} color="primary">
                {data?.teamSize ?? '—'}
              </Typography>
              <Typography variant="subtitle2" color="text.secondary" mt={0.5}>
                Team Size
              </Typography>
            </CardContent>
          </Card>
        </Grid>

        {/* Pending approvals card — click navigates to /approvals */}
        <Grid item xs={12} sm={6} md={3}>
          <Card variant="outlined" sx={{ borderColor: pendingCount > 0 ? 'warning.main' : undefined }}>
            <CardActionArea onClick={() => navigate('/approvals')}>
              <CardContent sx={{ textAlign: 'center', py: 3 }}>
                <Box display="flex" justifyContent="center" mb={1} color="warning.main">
                  <PendingActionsIcon fontSize="large" />
                </Box>
                <Typography variant="h3" fontWeight={700} color="warning.main">
                  {pendingCount}
                </Typography>
                <Typography variant="subtitle2" color="text.secondary" mt={0.5}>
                  Pending Approvals
                </Typography>
                <Button size="small" variant="outlined" sx={{ mt: 1.5 }}>
                  Review All
                </Button>
              </CardContent>
            </CardActionArea>
          </Card>
        </Grid>

        {/* Team pending requests table */}
        <Grid item xs={12}>
          <Card variant="outlined">
            <CardContent>
              <Typography variant="h6" mb={2}>
                Team Pending Requests
              </Typography>
              {teamPendingRequests.length === 0 ? (
                <Typography color="text.secondary">
                  No pending requests from your team.
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
                        <TableCell />
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {teamPendingRequests.map(req => (
                        <TableRow key={req.id}>
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
                          <TableCell>
                            <Button
                              size="small"
                              variant="outlined"
                              onClick={() => navigate('/approvals')}
                            >
                              Review
                            </Button>
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
