import { useState, useEffect, useCallback } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Paper,
  Snackbar,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableFooter,
  TableHead,
  TablePagination,
  TableRow,
  Typography,
  type ChipProps,
} from '@mui/material';
import {
  getMyLeaveRequests,
  cancelLeaveRequest,
  type LeaveRequestDto,
  type LeaveStatus,
} from '../../api/leaveRequestsApi';

// ---------------------------------------------------------------------------
// Status chip colour map
// ---------------------------------------------------------------------------

const STATUS_COLOR_MAP: Record<LeaveStatus, ChipProps['color']> = {
  Draft: 'default',
  Pending: 'warning',
  Approved: 'success',
  Rejected: 'error',
  Cancelled: 'default',
  Revoked: 'error',
};

function StatusChip({ status }: { status: LeaveStatus }) {
  return (
    <Chip
      label={status}
      color={STATUS_COLOR_MAP[status]}
      size="small"
      data-testid={`status-chip-${status}`}
    />
  );
}

// ---------------------------------------------------------------------------
// Date formatting helper
// ---------------------------------------------------------------------------

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    dateStyle: 'medium',
  });
}

// ---------------------------------------------------------------------------
// Page component
// ---------------------------------------------------------------------------

const PAGE_LIMIT = 10;

/**
 * LeaveHistoryPage — FR-20.
 *
 * Shows the authenticated employee's own leave requests, paginated.
 * Allows cancellation of Draft or Pending requests.
 *
 * Route: /leaves/history
 * Guard: ProtectedRoute (any authenticated user)
 */
export default function LeaveHistoryPage() {
  const [rows, setRows] = useState<LeaveRequestDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0); // zero-based for TablePagination
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);

  const [cancellingId, setCancellingId] = useState<string | null>(null);
  const [snackbar, setSnackbar] = useState<{ message: string; severity: 'success' | 'error' } | null>(null);

  // -------------------------------------------------------------------------
  // Data fetching
  // -------------------------------------------------------------------------

  const loadPage = useCallback(async (zeroBasedPage: number) => {
    setLoading(true);
    setFetchError(null);
    try {
      const result = await getMyLeaveRequests(zeroBasedPage + 1, PAGE_LIMIT);
      setRows(result.items);
      setTotal(result.total);
    } catch {
      setFetchError('Failed to load leave history. Please try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadPage(page);
  }, [page, loadPage]);

  // -------------------------------------------------------------------------
  // Cancel handler
  // -------------------------------------------------------------------------

  const handleCancel = async (id: string) => {
    setCancellingId(id);
    try {
      await cancelLeaveRequest(id);
      setSnackbar({ message: 'Leave request cancelled.', severity: 'success' });
      // Refetch current page so status updates
      void loadPage(page);
    } catch {
      setSnackbar({ message: 'Failed to cancel request. Please try again.', severity: 'error' });
    } finally {
      setCancellingId(null);
    }
  };

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------

  return (
    <Box p={4}>
      <Typography variant="h5" fontWeight={600} mb={3}>
        My Leave History
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

      {!loading && !fetchError && rows.length === 0 && (
        <Box display="flex" justifyContent="center" mt={6} data-testid="empty-state">
          <Typography color="text.secondary">No leave requests found.</Typography>
        </Box>
      )}

      {!loading && !fetchError && rows.length > 0 && (
        <TableContainer component={Paper} elevation={2}>
          <Table aria-label="leave history table" data-testid="leave-history-table">
            <TableHead>
              <TableRow>
                <TableCell><strong>Leave Type</strong></TableCell>
                <TableCell><strong>Start Date</strong></TableCell>
                <TableCell><strong>End Date</strong></TableCell>
                <TableCell align="center"><strong>Days</strong></TableCell>
                <TableCell><strong>Status</strong></TableCell>
                <TableCell align="center"><strong>Actions</strong></TableCell>
              </TableRow>
            </TableHead>

            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.id} data-testid={`leave-row-${row.id}`}>
                  <TableCell>{row.leaveTypeName}</TableCell>
                  <TableCell>{formatDate(row.startDate)}</TableCell>
                  <TableCell>{formatDate(row.endDate)}</TableCell>
                  <TableCell align="center">{row.computedDays}</TableCell>
                  <TableCell>
                    <StatusChip status={row.status} />
                  </TableCell>
                  <TableCell align="center">
                    {(row.status === 'Pending' || row.status === 'Draft') && (
                      <Button
                        variant="outlined"
                        size="small"
                        color="error"
                        disabled={cancellingId === row.id}
                        onClick={() => void handleCancel(row.id)}
                        data-testid={`cancel-btn-${row.id}`}
                      >
                        {cancellingId === row.id ? (
                          <CircularProgress size={14} color="inherit" />
                        ) : (
                          'Cancel'
                        )}
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>

            <TableFooter>
              <TableRow>
                <TablePagination
                  count={total}
                  page={page}
                  rowsPerPage={PAGE_LIMIT}
                  rowsPerPageOptions={[PAGE_LIMIT]}
                  onPageChange={(_event, newPage) => setPage(newPage)}
                  data-testid="pagination"
                />
              </TableRow>
            </TableFooter>
          </Table>
        </TableContainer>
      )}

      <Snackbar
        open={snackbar !== null}
        autoHideDuration={5000}
        onClose={() => setSnackbar(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          onClose={() => setSnackbar(null)}
          severity={snackbar?.severity ?? 'info'}
          sx={{ width: '100%' }}
        >
          {snackbar?.message}
        </Alert>
      </Snackbar>
    </Box>
  );
}
