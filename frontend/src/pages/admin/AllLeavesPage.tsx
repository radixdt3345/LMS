import { useState, useEffect, useCallback } from 'react';
import { apiClient } from '../../api/axiosClient';
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
  getAllLeaveRequests,
  revokeLeaveRequest,
  type LeaveStatus,
} from '../../api/leaveRequestsApi';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface AdminLeaveRow {
  id: string;
  employeeName: string;
  leaveTypeName: string;
  startDate: string;
  endDate: string;
  computedDays: number;
  status: LeaveStatus;
}

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
// Date formatting
// ---------------------------------------------------------------------------

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { dateStyle: 'medium' });
}

// ---------------------------------------------------------------------------
// Page component
// ---------------------------------------------------------------------------

const PAGE_LIMIT = 10;

/**
 * AllLeavesPage — FR-22, FR-23.
 *
 * HR Admin / Super Admin view of all leave requests across the organisation.
 * Supports pagination, revoking Approved requests, and CSV export.
 *
 * Route: /admin/leaves
 * Guard: RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}
 */
export default function AllLeavesPage() {
  const [rows, setRows] = useState<AdminLeaveRow[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0); // zero-based for TablePagination
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);

  const [revokingId, setRevokingId] = useState<string | null>(null);
  const [snackbar, setSnackbar] = useState<{
    message: string;
    severity: 'success' | 'error';
  } | null>(null);

  // -------------------------------------------------------------------------
  // Data fetching
  // -------------------------------------------------------------------------

  const loadPage = useCallback(async (zeroBasedPage: number) => {
    setLoading(true);
    setFetchError(null);
    try {
      const result = await getAllLeaveRequests(zeroBasedPage + 1, PAGE_LIMIT);
      const mapped: AdminLeaveRow[] = result.items.map((item) => ({
        id: item.id,
        employeeName: item.employeeName,
        leaveTypeName: item.leaveTypeName,
        startDate: item.startDate,
        endDate: item.endDate,
        computedDays: item.computedDays,
        status: item.status,
      }));
      setRows(mapped);
      setTotal(result.total);
    } catch {
      setFetchError('Failed to load leave requests. Please try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadPage(page);
  }, [page, loadPage]);

  // -------------------------------------------------------------------------
  // Revoke handler
  // -------------------------------------------------------------------------

  const handleRevoke = async (id: string) => {
    setRevokingId(id);
    try {
      await revokeLeaveRequest(id);
      setSnackbar({ message: 'Leave request revoked.', severity: 'success' });
      void loadPage(page);
    } catch {
      setSnackbar({
        message: 'Failed to revoke request. Please try again.',
        severity: 'error',
      });
    } finally {
      setRevokingId(null);
    }
  };

  // -------------------------------------------------------------------------
  // CSV export
  // -------------------------------------------------------------------------

  const handleExportCsv = async () => {
    try {
      const response = await apiClient.get('/api/v1/reports/export', {
        responseType: 'blob',
      });
      const blob = new Blob([response.data as BlobPart], { type: 'text/csv' });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `leave-report-${new Date().toISOString().slice(0, 10)}.csv`;
      document.body.appendChild(anchor);
      anchor.click();
      document.body.removeChild(anchor);
      URL.revokeObjectURL(url);
    } catch {
      setSnackbar({ message: 'CSV export failed. Please try again.', severity: 'error' });
    }
  };

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------

  return (
    <Box p={4}>
      <Box display="flex" alignItems="center" justifyContent="space-between" mb={3}>
        <Typography variant="h5" fontWeight={600}>
          All Leave Requests
        </Typography>
        <Button
          variant="outlined"
          onClick={() => { void handleExportCsv(); }}
          data-testid="export-csv-button"
        >
          Export CSV
        </Button>
      </Box>

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
          <Table aria-label="all leave requests table" data-testid="all-leaves-table">
            <TableHead>
              <TableRow>
                <TableCell><strong>Employee</strong></TableCell>
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
                  <TableCell>{row.employeeName}</TableCell>
                  <TableCell>{row.leaveTypeName}</TableCell>
                  <TableCell>{formatDate(row.startDate)}</TableCell>
                  <TableCell>{formatDate(row.endDate)}</TableCell>
                  <TableCell align="center">{row.computedDays}</TableCell>
                  <TableCell>
                    <StatusChip status={row.status} />
                  </TableCell>
                  <TableCell align="center">
                    {row.status === 'Approved' && (
                      <Button
                        variant="outlined"
                        size="small"
                        color="error"
                        disabled={revokingId === row.id}
                        onClick={() => void handleRevoke(row.id)}
                        data-testid={`revoke-btn-${row.id}`}
                      >
                        {revokingId === row.id ? (
                          <CircularProgress size={14} color="inherit" />
                        ) : (
                          'Revoke'
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
