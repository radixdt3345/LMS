import { useCallback, useEffect, useState } from 'react';
import { Alert, Box, Button, Card, CardContent, Chip, CircularProgress, Divider, Grid, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material';
import { submitCompOffRequest, getMyCompOffRequests, getMyCompOffCredits, type CompOffRequestDto, type CompOffCreditDto } from '../api/compOffApi';

const STATUS_COLOUR_MAP: Record<string, 'default' | 'warning' | 'success' | 'error'> = { Pending: 'warning', Approved: 'success', Rejected: 'error' };
function daysRemaining(expiresAt: string) { return Math.max(Math.ceil((new Date(expiresAt).getTime() - Date.now()) / 86400000), 0); }

export default function CompOffPage() {
  const [workedDate, setWorkedDate] = useState('');
  const [workedHours, setWorkedHours] = useState('8');
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitSuccess, setSubmitSuccess] = useState(false);
  const [formErrors, setFormErrors] = useState<Record<string, string>>({});
  const [requests, setRequests] = useState<CompOffRequestDto[]>([]);
  const [credits, setCredits] = useState<CompOffCreditDto[]>([]);
  const [loadingData, setLoadingData] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setLoadingData(true); setLoadError(null);
    try { const [reqs, creds] = await Promise.all([getMyCompOffRequests(), getMyCompOffCredits()]); setRequests(reqs); setCredits(creds); }
    catch { setLoadError('Failed to load comp-off data.'); }
    finally { setLoadingData(false); }
  }, []);

  useEffect(() => { void loadData(); }, [loadData]);

  const handleSubmit = async () => {
    const errs: Record<string, string> = {};
    if (!workedDate) errs.workedDate = 'Worked date is required.';
    const hours = parseFloat(workedHours);
    if (isNaN(hours) || hours < 4) errs.workedHours = 'Minimum 4 hours required.';
    setFormErrors(errs);
    if (Object.keys(errs).length > 0) return;
    setSubmitting(true); setSubmitError(null); setSubmitSuccess(false);
    try { await submitCompOffRequest({ workedDate, workedHours: hours }); setSubmitSuccess(true); setWorkedDate(''); setWorkedHours('8'); void loadData(); }
    catch (err: unknown) {
      const axErr = err as { response?: { status: number; data?: { error?: { message?: string } } } };
      setSubmitError(axErr.response?.status === 409 ? 'A comp-off request already exists for that date.' : axErr.response?.data?.error?.message ?? 'Failed to submit comp-off request.');
    }
    finally { setSubmitting(false); }
  };

  const totalAvailableCredits = credits.filter(c => daysRemaining(c.expiresAt) > 0).reduce((sum, c) => sum + c.creditDays - c.usedDays, 0);

  return (
    <Box p={4}>
      <Typography variant="h4" mb={3}>Comp-Off Management</Typography>
      <Grid container spacing={3}>
        <Grid item xs={12} md={5}>
          <Card variant="outlined"><CardContent>
            <Typography variant="h6" mb={2}>Submit Comp-Off Request</Typography>
            {submitSuccess && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSubmitSuccess(false)}>Comp-off request submitted successfully.</Alert>}
            {submitError && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setSubmitError(null)}>{submitError}</Alert>}
            <TextField label="Worked Date" type="date" fullWidth sx={{ mb: 2 }} InputLabelProps={{ shrink: true }} value={workedDate} onChange={e => { setWorkedDate(e.target.value); setFormErrors(p => ({ ...p, workedDate: '' })); }} error={!!formErrors.workedDate} helperText={formErrors.workedDate} />
            <TextField label="Hours Worked" type="number" fullWidth sx={{ mb: 3 }} value={workedHours} onChange={e => { setWorkedHours(e.target.value); setFormErrors(p => ({ ...p, workedHours: '' })); }} error={!!formErrors.workedHours} helperText={formErrors.workedHours ?? '4h = 0.5 day  |  8h = 1 day'} inputProps={{ min: 4, step: 0.5 }} />
            <Button variant="contained" fullWidth onClick={() => void handleSubmit()} disabled={submitting}>{submitting ? <CircularProgress size={20} color="inherit" /> : 'Submit Request'}</Button>
          </CardContent></Card>
          <Card variant="outlined" sx={{ mt: 2 }}><CardContent>
            <Typography variant="h6" mb={1}>Available Credits</Typography>
            <Typography variant="h3" fontWeight={700} color="primary">{loadingData ? '…' : totalAvailableCredits.toFixed(1)}</Typography>
            <Typography variant="body2" color="text.secondary">days available (not expired, not used)</Typography>
          </CardContent></Card>
        </Grid>
        <Grid item xs={12} md={7}>
          {loadError && <Alert severity="error" sx={{ mb: 2 }}>{loadError}</Alert>}
          <Card variant="outlined" sx={{ mb: 2 }}><CardContent>
            <Typography variant="h6" mb={2}>My Comp-Off Requests</Typography>
            {loadingData ? <Box display="flex" justifyContent="center" py={3}><CircularProgress /></Box>
              : requests.length === 0 ? <Typography color="text.secondary">No comp-off requests yet.</Typography>
              : <TableContainer component={Paper} elevation={0}><Table size="small"><TableHead><TableRow><TableCell>Worked Date</TableCell><TableCell align="center">Hours</TableCell><TableCell>Status</TableCell><TableCell>Submitted</TableCell></TableRow></TableHead><TableBody>{requests.map(r => (<TableRow key={r.id}><TableCell>{r.workedDate}</TableCell><TableCell align="center">{r.workedHours}</TableCell><TableCell><Chip label={r.status} size="small" color={STATUS_COLOUR_MAP[r.status] ?? 'default'} /></TableCell><TableCell>{new Date(r.createdAt).toLocaleDateString()}</TableCell></TableRow>))}</TableBody></Table></TableContainer>}
          </CardContent></Card>
          <Divider />
          <Card variant="outlined" sx={{ mt: 2 }}><CardContent>
            <Typography variant="h6" mb={2}>My Comp-Off Credits</Typography>
            {loadingData ? <Box display="flex" justifyContent="center" py={3}><CircularProgress /></Box>
              : credits.length === 0 ? <Typography color="text.secondary">No credits yet.</Typography>
              : <TableContainer component={Paper} elevation={0}><Table size="small"><TableHead><TableRow><TableCell align="center">Credit</TableCell><TableCell align="center">Used</TableCell><TableCell align="center">Remaining</TableCell><TableCell>Expires</TableCell><TableCell>Status</TableCell></TableRow></TableHead><TableBody>{credits.map(c => { const daysLeft = daysRemaining(c.expiresAt); return (<TableRow key={c.id}><TableCell align="center">{c.creditDays}</TableCell><TableCell align="center">{c.usedDays}</TableCell><TableCell align="center">{(c.creditDays - c.usedDays).toFixed(1)}</TableCell><TableCell>{c.expiresAt}</TableCell><TableCell><Chip label={daysLeft === 0 ? 'Expired' : `${daysLeft}d left`} size="small" color={daysLeft === 0 ? 'error' : daysLeft <= 30 ? 'warning' : 'success'} /></TableCell></TableRow>); })}</TableBody></Table></TableContainer>}
          </CardContent></Card>
        </Grid>
      </Grid>
    </Box>
  );
}
