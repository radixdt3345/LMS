/**
 * CompOffPage — Employee, Manager
 *
 * Allows an employee/manager to:
 *  1. Submit a comp-off request (worked on a non-working day).
 *  2. View their own comp-off requests (with status).
 *  3. View their comp-off credit balance (with expiry info).
 *
 * Route: /comp-off
 * Guard: ProtectedRoute (any authenticated user — HR Admin sees this too for visibility)
 */
import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Divider,
  Grid,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import {
  submitCompOffRequest,
  getMyCompOffRequests,
  getMyCompOffCredits,
  type CompOffRequestDto,
  type CompOffCreditDto,
} from '../api/compOffApi';

const STATUS_COLOUR_MAP: Record<
  string,
  'default' | 'warning' | 'success' | 'error'
> = {
  Pending: 'warning',
  Approved: 'success',
  Rejected: 'error',
};

function daysRemaining(expiresAt: string): number {
  const exp = new Date(expiresAt);
  const now = new Date();
  const diff = Math.ceil((exp.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));
  return Math.max(diff, 0);
}

export default function CompOffPage() {
  // Form
  const [workedDate, setWorkedDate] = useState('');
  const [workedHours, setWorkedHours] = useState('8');
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitSuccess, setSubmitSuccess] = useState(false);
  const [formErrors, setFormErrors] = useState<Record<string, string>>({});

  // Data
  const [requests, setRequests] = useState<CompOffRequestDto[]>([]);
  const [credits, setCredits] = useState<CompOffCreditDto[]>([]);
  const [loadingData, setLoadingData] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setLoadingData(true);
    setLoadError(null);
    try {
      const [reqs, creds] = await Promise.all([
        getMyCompOffRequests(),
        getMyCompOffCredits(),
      ]);
      setRequests(reqs);
      setCredits(creds);
    } catch {
      setLoadError('Failed to load comp-off data.');
    } finally {
      setLoadingData(false);
    }
  }, []);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const validateForm = (): boolean => {
    const errs: Record<string, string> = {};
    if (!workedDate) errs.workedDate = 'Worked date is required.';
    const hours = parseFloat(workedHours);
    if (isNaN(hours) || hours < 4) {
      errs.workedHours = 'Minimum 4 hours required. 4h = 0.5 day; 8h = 1 day.';
    }
    setFormErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handleSubmit = async () => {
    if (!validateForm()) return;
    setSubmitting(true);
    setSubmitError(null);
    setSubmitSuccess(false);
    try {
      await submitCompOffRequest({
        workedDate,
        workedHours: parseFloat(workedHours),
      });
      setSubmitSuccess(true);
      setWorkedDate('');
      setWorkedHours('8');
      void loadData();
    } catch (err: unknown) {
      const axErr = err as { response?: { status: number; data?: { error?: { message?: string } } } };
      if (axErr.response?.status === 409) {
        setSubmitError('A comp-off request already exists for that date.');
      } else if (axErr.response?.status === 422) {
        setSubmitError(
          axErr.response.data?.error?.message ??
            'Invalid hours or worked date is a regular working day.',
        );
      } else {
        setSubmitError('Failed to submit comp-off request. Please try again.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  // Active credits total
  const totalAvailableCredits = credits
    .filter(c => daysRemaining(c.expiresAt) > 0)
    .reduce((sum, c) => sum + c.creditDays - c.usedDays, 0);

  return (
    <Box p={4}>
      <Typography variant="h4" mb={3}>
        Comp-Off Management
      </Typography>

      <Grid container spacing={3}>
        {/* Submit form */}
        <Grid item xs={12} md={5}>
          <Card variant="outlined">
            <CardContent>
              <Typography variant="h6" mb={2}>
                Submit Comp-Off Request
              </Typography>
              <Typography variant="body2" color="text.secondary" mb={2}>
                Submit a request for a day you worked on a non-working day (weekend / holiday).
                Minimum 4 hours. Credits expire 180 days after the worked date.
              </Typography>

              {submitSuccess && (
                <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSubmitSuccess(false)}>
                  Comp-off request submitted successfully.
                </Alert>
              )}
              {submitError && (
                <Alert severity="error" sx={{ mb: 2 }} onClose={() => setSubmitError(null)}>
                  {submitError}
                </Alert>
              )}

              <TextField
                label="Worked Date"
                type="date"
                fullWidth
                sx={{ mb: 2 }}
                InputLabelProps={{ shrink: true }}
                value={workedDate}
                onChange={e => {
                  setWorkedDate(e.target.value);
                  setFormErrors(prev => ({ ...prev, workedDate: '' }));
                }}
                error={!!formErrors.workedDate}
                helperText={formErrors.workedDate}
                inputProps={{ 'data-testid': 'worked-date-input' }}
              />

              <TextField
                label="Hours Worked"
                type="number"
                fullWidth
                sx={{ mb: 3 }}
                value={workedHours}
                onChange={e => {
                  setWorkedHours(e.target.value);
                  setFormErrors(prev => ({ ...prev, workedHours: '' }));
                }}
                error={!!formErrors.workedHours}
                helperText={
                  formErrors.workedHours ?? '4h = 0.5 day credit  |  8h = 1 day credit'
                }
                inputProps={{ min: 4, step: 0.5, 'data-testid': 'worked-hours-input' }}
              />

              <Button
                variant="contained"
                fullWidth
                onClick={() => void handleSubmit()}
                disabled={submitting}
                data-testid="submit-compoff-btn"
              >
                {submitting ? <CircularProgress size={20} color="inherit" /> : 'Submit Request'}
              </Button>
            </CardContent>
          </Card>

          {/* Credit summary */}
          <Card variant="outlined" sx={{ mt: 2 }}>
            <CardContent>
              <Typography variant="h6" mb={1}>
                Available Credits
              </Typography>
              <Typography variant="h3" fontWeight={700} color="primary">
                {loadingData ? '…' : totalAvailableCredits.toFixed(1)}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                days available (not expired, not used)
              </Typography>
            </CardContent>
          </Card>
        </Grid>

        {/* Right column: requests + credits */}
        <Grid item xs={12} md={7}>
          {loadError && <Alert severity="error" sx={{ mb: 2 }}>{loadError}</Alert>}

          {/* Requests table */}
          <Card variant="outlined" sx={{ mb: 2 }}>
            <CardContent>
              <Typography variant="h6" mb={2}>
                My Comp-Off Requests
              </Typography>
              {loadingData ? (
                <Box display="flex" justifyContent="center" py={3}>
                  <CircularProgress />
                </Box>
              ) : requests.length === 0 ? (
                <Typography color="text.secondary">No comp-off requests yet.</Typography>
              ) : (
                <TableContainer component={Paper} elevation={0}>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Worked Date</TableCell>
                        <TableCell align="center">Hours</TableCell>
                        <TableCell>Status</TableCell>
                        <TableCell>Submitted</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {requests.map(r => (
                        <TableRow key={r.id}>
                          <TableCell>{r.workedDate}</TableCell>
                          <TableCell align="center">{r.workedHours}</TableCell>
                          <TableCell>
                            <Chip
                              label={r.status}
                              size="small"
                              color={STATUS_COLOUR_MAP[r.status] ?? 'default'}
                            />
                          </TableCell>
                          <TableCell>
                            {new Date(r.createdAt).toLocaleDateString()}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              )}
            </CardContent>
          </Card>

          <Divider />

          {/* Credits table */}
          <Card variant="outlined" sx={{ mt: 2 }}>
            <CardContent>
              <Typography variant="h6" mb={2}>
                My Comp-Off Credits
              </Typography>
              {loadingData ? (
                <Box display="flex" justifyContent="center" py={3}>
                  <CircularProgress />
                </Box>
              ) : credits.length === 0 ? (
                <Typography color="text.secondary">No credits yet.</Typography>
              ) : (
                <TableContainer component={Paper} elevation={0}>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell align="center">Credit (days)</TableCell>
                        <TableCell align="center">Used</TableCell>
                        <TableCell align="center">Remaining</TableCell>
                        <TableCell>Expires</TableCell>
                        <TableCell>Status</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {credits.map(c => {
                        const rem = c.creditDays - c.usedDays;
                        const daysLeft = daysRemaining(c.expiresAt);
                        const expired = daysLeft === 0;
                        return (
                          <TableRow key={c.id}>
                            <TableCell align="center">{c.creditDays}</TableCell>
                            <TableCell align="center">{c.usedDays}</TableCell>
                            <TableCell align="center">{rem.toFixed(1)}</TableCell>
                            <TableCell>{c.expiresAt}</TableCell>
                            <TableCell>
                              {expired ? (
                                <Chip label="Expired" size="small" color="error" />
                              ) : daysLeft <= 30 ? (
                                <Chip
                                  label={`${daysLeft}d left`}
                                  size="small"
                                  color="warning"
                                />
                              ) : (
                                <Chip
                                  label={`${daysLeft}d left`}
                                  size="small"
                                  color="success"
                                />
                              )}
                            </TableCell>
                          </TableRow>
                        );
                      })}
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
