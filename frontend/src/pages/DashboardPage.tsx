import { useEffect, useState } from 'react';
import {
  Box,
  Typography,
  Grid,
  CircularProgress,
  Alert,
} from '@mui/material';
import LeaveBalanceCard from '../components/LeaveBalanceCard';
import CompOffExpiryWarning from '../components/CompOffExpiryWarning';
import {
  fetchMyLeaveBalances,
  fetchMyCompOffCredits,
  BalanceItem,
  CompOffCredit,
} from '../api/leaveBalanceApi';

/**
 * Employee / Manager dashboard.
 * Shows leave balance cards (one per leave type) and a comp-off expiry warning
 * when credits are due to expire within 30 days.
 * LEAVECORE-UI-002 / T-033.
 */
export default function DashboardPage() {
  const [balances, setBalances] = useState<BalanceItem[]>([]);
  const [credits,  setCredits]  = useState<CompOffCredit[]>([]);
  const [loading,  setLoading]  = useState(true);
  const [error,    setError]    = useState<string | null>(null);

  useEffect(() => {
    Promise.all([
      fetchMyLeaveBalances(),
      fetchMyCompOffCredits(),
    ])
      .then(([b, c]) => {
        setBalances(b);
        setCredits(c);
      })
      .catch(() =>
        setError('Failed to load leave data. Please try again later.'),
      )
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight={200}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box p={4}>
      <Typography variant="h4" mb={3}>
        Dashboard
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <CompOffExpiryWarning credits={credits} />

      <Typography variant="h6" mb={2}>
        My Leave Balances
      </Typography>

      {!error && balances.length === 0 ? (
        <Typography color="text.secondary">
          No leave balances found for the current year.
        </Typography>
      ) : (
        <Grid container spacing={2}>
          {balances.map((b) => (
            <Grid item xs={12} sm={6} md={4} key={b.leaveTypeId}>
              <LeaveBalanceCard balance={b} />
            </Grid>
          ))}
        </Grid>
      )}
    </Box>
  );
}
