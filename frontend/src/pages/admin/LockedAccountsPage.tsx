import { useState, useEffect, useCallback } from 'react';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Paper,
  Snackbar,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import {
  fetchLockedAccounts,
  unlockAccount,
  type LockedAccount,
} from '../../api/adminApi';

/** Format an ISO-8601 UTC timestamp for display in the local timezone. */
function formatTimestamp(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
}

/**
 * LockedAccountsPage — FR-7, FR-8.
 *
 * Displays all currently locked user accounts and allows HR Admin / Super Admin
 * to unlock them with a single click. Optimistic UI: the row is removed
 * immediately on success; any API error surfaces via a Snackbar.
 *
 * Route: /admin/users/locked
 * Guard: RoleProtectedRoute allowedRoles={['HRAdmin','SuperAdmin']}
 */
export default function LockedAccountsPage() {
  const [accounts, setAccounts] = useState<LockedAccount[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);
  const [snackbarMessage, setSnackbarMessage] = useState<string | null>(null);
  const [unlockingId, setUnlockingId] = useState<string | null>(null);

  const loadAccounts = useCallback(async () => {
    setLoading(true);
    setFetchError(null);
    try {
      const result = await fetchLockedAccounts();
      setAccounts(result.items);
    } catch {
      setFetchError('Failed to load locked accounts. Please try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadAccounts();
  }, [loadAccounts]);

  const handleUnlock = async (id: string) => {
    // Optimistic update — remove row immediately
    setAccounts((prev) => prev.filter((a) => a.id !== id));
    setUnlockingId(id);
    try {
      await unlockAccount(id);
    } catch {
      // Rollback: re-fetch so the row reappears
      setSnackbarMessage('Failed to unlock account. Please try again.');
      void loadAccounts();
    } finally {
      setUnlockingId(null);
    }
  };

  const handleSnackbarClose = () => setSnackbarMessage(null);

  return (
    <Box p={4}>
      <Typography variant="h5" fontWeight={600} mb={3}>
        Locked Accounts
      </Typography>

      {loading && (
        <Box display="flex" justifyContent="center" mt={6}>
          <CircularProgress />
        </Box>
      )}

      {!loading && fetchError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {fetchError}
        </Alert>
      )}

      {!loading && !fetchError && accounts.length === 0 && (
        <Box
          display="flex"
          alignItems="center"
          justifyContent="center"
          mt={6}
          data-testid="empty-state"
        >
          <Typography color="text.secondary" variant="body1">
            No locked accounts
          </Typography>
        </Box>
      )}

      {!loading && !fetchError && accounts.length > 0 && (
        <TableContainer component={Paper} elevation={2}>
          <Table
            aria-label="locked accounts table"
            data-testid="accounts-table"
          >
            <TableHead>
              <TableRow>
                <TableCell>
                  <strong>Name</strong>
                </TableCell>
                <TableCell>
                  <strong>Email</strong>
                </TableCell>
                <TableCell>
                  <strong>Locked Since</strong>
                </TableCell>
                <TableCell align="center">
                  <strong>Failed Attempts</strong>
                </TableCell>
                <TableCell align="center">
                  <strong>Actions</strong>
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {accounts.map((account) => (
                <TableRow key={account.id} data-testid={`row-${account.id}`}>
                  <TableCell>
                    {account.firstName} {account.lastName}
                  </TableCell>
                  <TableCell>{account.email}</TableCell>
                  <TableCell>{formatTimestamp(account.lockoutUntil)}</TableCell>
                  <TableCell align="center">
                    {account.failedLoginCount}
                  </TableCell>
                  <TableCell align="center">
                    <Button
                      variant="outlined"
                      size="small"
                      color="primary"
                      disabled={unlockingId === account.id}
                      onClick={() => void handleUnlock(account.id)}
                      data-testid={`unlock-btn-${account.id}`}
                    >
                      Unlock
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Snackbar
        open={snackbarMessage !== null}
        autoHideDuration={5000}
        onClose={handleSnackbarClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          onClose={handleSnackbarClose}
          severity="error"
          sx={{ width: '100%' }}
        >
          {snackbarMessage}
        </Alert>
      </Snackbar>
    </Box>
  );
}
