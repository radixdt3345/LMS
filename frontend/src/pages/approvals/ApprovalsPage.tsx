import { useState, useEffect, useCallback, useMemo } from 'react';
import {
  DataGrid,
  type GridColDef,
  type GridRenderCellParams,
  type GridRowParams,
} from '@mui/x-data-grid';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Link as MuiLink,
  Snackbar,
  TextField,
  Typography,
  type ChipProps,
} from '@mui/material';
import {
  getPendingApprovals,
  approveRequest,
  rejectRequest,
  type LeaveRequestDto,
} from '../../api/approvalsApi';
import type { LeaveStatus } from '../../api/leaveRequestsApi';

// ---------------------------------------------------------------------------
// Status chip
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
      color={STATUS_COLOR_MAP[status] ?? 'default'}
      size="small"
    />
  );
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { dateStyle: 'medium' });
}

const PAGE_LIMIT = 10;

// ---------------------------------------------------------------------------
// Page component
// ---------------------------------------------------------------------------

/**
 * ApprovalsPage — LEAVECORE-UI-005.
 *
 * Approval inbox for Managers and HR Admins. Displays pending leave requests
 * with approve / reject actions and a row-click detail view.
 *
 * Route: /approvals
 * Guard: RoleProtectedRoute allowedRoles={['Manager', 'HRAdmin', 'SuperAdmin']}
 */
export default function ApprovalsPage() {
  // ── Grid state ────────────────────────────────────────────────────────────
  const [rows, setRows] = useState<LeaveRequestDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0); // zero-based (DataGrid v5 convention)
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  // ── Detail dialog ─────────────────────────────────────────────────────────
  const [selectedRow, setSelectedRow] = useState<LeaveRequestDto | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);

  // ── Reject dialog ─────────────────────────────────────────────────────────
  const [rejectDialogOpen, setRejectDialogOpen] = useState(false);
  const [rejectTargetId, setRejectTargetId] = useState<string | null>(null);
  const [rejectComment, setRejectComment] = useState('');
  const [rejectLoading, setRejectLoading] = useState(false);

  // ── Toast ─────────────────────────────────────────────────────────────────
  const [snackbar, setSnackbar] = useState<{
    message: string;
    severity: 'success' | 'error';
  } | null>(null);

  // ── Data fetching ─────────────────────────────────────────────────────────

  const loadPage = useCallback(async (zeroBasedPage: number) => {
    setLoading(true);
    setFetchError(null);
    try {
      const result = await getPendingApprovals(zeroBasedPage + 1, PAGE_LIMIT);
      setRows(result.items);
      setTotal(result.total);
    } catch {
      setFetchError('Failed to load pending approvals. Please try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadPage(page);
  }, [page, loadPage]);

  // ── Approve (optimistic update) ───────────────────────────────────────────

  const handleApprove = useCallback(
    async (id: string) => {
      const stashedIndex = rows.findIndex((r) => r.id === id);
      const stashed = rows[stashedIndex];

      // Optimistic: remove row immediately from grid
      setRows((prev) => prev.filter((r) => r.id !== id));
      setActionError(null);

      try {
        await approveRequest(id);
        setSnackbar({ message: 'Leave request approved.', severity: 'success' });
      } catch {
        // Restore the row at its original position on failure
        if (stashed !== undefined) {
          const capturedIdx = stashedIndex;
          const capturedItem = stashed;
          setRows((prev) => {
            const copy = [...prev];
            copy.splice(Math.min(capturedIdx, copy.length), 0, capturedItem);
            return copy;
          });
        }
        setActionError('Failed to approve request. Please try again.');
      }
    },
    [rows],
  );

  // ── Open reject dialog ────────────────────────────────────────────────────

  const handleOpenReject = useCallback((id: string) => {
    setRejectTargetId(id);
    setRejectComment('');
    setActionError(null);
    setRejectDialogOpen(true);
  }, []);

  // ── Submit reject ─────────────────────────────────────────────────────────

  const handleRejectSubmit = async () => {
    if (!rejectTargetId || !rejectComment.trim()) return;
    setRejectLoading(true);
    setActionError(null);
    try {
      await rejectRequest(rejectTargetId, rejectComment.trim());
      setRows((prev) => prev.filter((r) => r.id !== rejectTargetId));
      setSnackbar({ message: 'Leave request rejected.', severity: 'success' });
      setRejectDialogOpen(false);
    } catch {
      setActionError('Failed to reject request. Please try again.');
    } finally {
      setRejectLoading(false);
    }
  };

  // ── Row click → detail dialog ─────────────────────────────────────────────

  const handleRowClick = useCallback((params: GridRowParams) => {
    setSelectedRow(params.row as LeaveRequestDto);
    setDetailOpen(true);
  }, []);

  // ── Columns ───────────────────────────────────────────────────────────────

  const columns = useMemo<GridColDef[]>(
    () => [
      { field: 'employeeName', headerName: 'Employee', flex: 1, minWidth: 140 },
      { field: 'leaveTypeName', headerName: 'Leave Type', flex: 1, minWidth: 120 },
      {
        field: 'startDate',
        headerName: 'Start',
        width: 120,
        valueFormatter: ({ value }) => formatDate(value as string),
      },
      {
        field: 'endDate',
        headerName: 'End',
        width: 120,
        valueFormatter: ({ value }) => formatDate(value as string),
      },
      {
        field: 'computedDays',
        headerName: 'Days',
        width: 80,
        align: 'center',
        headerAlign: 'center',
      },
      {
        field: 'isRetroactive',
        headerName: 'Retroactive',
        width: 130,
        sortable: false,
        renderCell: (params: GridRenderCellParams) =>
          params.value === true ? (
            <Chip
              label="Retroactive"
              size="small"
              sx={{ bgcolor: 'warning.light', color: 'warning.dark' }}
              data-testid={`retroactive-chip-${String(params.row.id)}`}
            />
          ) : null,
      },
      {
        field: 'status',
        headerName: 'Status',
        width: 110,
        renderCell: (params: GridRenderCellParams) => (
          <StatusChip status={params.value as LeaveStatus} />
        ),
      },
      {
        field: 'actions',
        headerName: 'Actions',
        width: 200,
        sortable: false,
        disableColumnMenu: true,
        renderCell: (params: GridRenderCellParams) => (
          <Box display="flex" gap={1} alignItems="center" height="100%">
            <Button
              size="small"
              variant="contained"
              color="success"
              onClick={(e) => {
                e.stopPropagation();
                void handleApprove(String(params.row.id));
              }}
              data-testid={`approve-btn-${String(params.row.id)}`}
            >
              Approve
            </Button>
            <Button
              size="small"
              variant="outlined"
              color="error"
              onClick={(e) => {
                e.stopPropagation();
                handleOpenReject(String(params.row.id));
              }}
              data-testid={`reject-btn-${String(params.row.id)}`}
            >
              Reject
            </Button>
          </Box>
        ),
      },
    ],
    [handleApprove, handleOpenReject],
  );

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <Box p={4}>
      <Typography variant="h5" fontWeight={600} mb={3}>
        Approval Inbox
      </Typography>

      {/* Fetch error */}
      {fetchError && (
        <Alert severity="error" sx={{ mb: 2 }} data-testid="fetch-error">
          {fetchError}
        </Alert>
      )}

      {/* Action error (approve / reject failures) */}
      {actionError && (
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          onClose={() => setActionError(null)}
          data-testid="action-error"
        >
          {actionError}
        </Alert>
      )}

      {/* Approvals grid */}
      <Box sx={{ bgcolor: 'background.paper' }} data-testid="approvals-grid-wrapper">
        <DataGrid
          rows={rows}
          columns={columns}
          pagination
          paginationMode="server"
          rowCount={total}
          page={page}
          pageSize={PAGE_LIMIT}
          rowsPerPageOptions={[PAGE_LIMIT]}
          onPageChange={(newPage) => setPage(newPage)}
          onRowClick={handleRowClick}
          loading={loading}
          autoHeight
          disableSelectionOnClick
          sx={{ cursor: 'pointer' }}
        />
      </Box>

      {/* ── Detail Dialog ─────────────────────────────────────────────────── */}
      <Dialog
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        maxWidth="sm"
        fullWidth
        data-testid="detail-dialog"
      >
        {selectedRow && (
          <>
            <DialogTitle>
              Leave Request &mdash; {selectedRow.employeeName}
              {selectedRow.isRetroactive && (
                <Chip
                  label="Retroactive"
                  size="small"
                  sx={{ ml: 1, bgcolor: 'warning.light', color: 'warning.dark' }}
                />
              )}
            </DialogTitle>
            <DialogContent dividers>
              <Box
                display="grid"
                gridTemplateColumns="1fr 1fr"
                gap={2}
              >
                <Box>
                  <Typography variant="caption" color="text.secondary">
                    Leave Type
                  </Typography>
                  <Typography variant="body2" fontWeight={500}>
                    {selectedRow.leaveTypeName}
                  </Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">
                    Status
                  </Typography>
                  <Box mt={0.5}>
                    <StatusChip status={selectedRow.status} />
                  </Box>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">
                    Start Date
                  </Typography>
                  <Typography variant="body2">
                    {formatDate(selectedRow.startDate)}
                  </Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">
                    End Date
                  </Typography>
                  <Typography variant="body2">
                    {formatDate(selectedRow.endDate)}
                  </Typography>
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary">
                    Computed Days
                  </Typography>
                  <Typography variant="body2">
                    {selectedRow.computedDays}
                  </Typography>
                </Box>
              </Box>

              <Box mt={2}>
                <Typography variant="caption" color="text.secondary">
                  Reason
                </Typography>
                <Typography variant="body2" mt={0.5}>
                  {selectedRow.reason}
                </Typography>
              </Box>

              {selectedRow.documentUrl && (
                <Box mt={2}>
                  <Typography variant="caption" color="text.secondary">
                    Supporting Document
                  </Typography>
                  <Box mt={0.5}>
                    <MuiLink
                      href={selectedRow.documentUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      data-testid="document-link"
                    >
                      View Document
                    </MuiLink>
                  </Box>
                </Box>
              )}
            </DialogContent>
            <DialogActions>
              <Button onClick={() => setDetailOpen(false)}>Close</Button>
            </DialogActions>
          </>
        )}
      </Dialog>

      {/* ── Reject Dialog ─────────────────────────────────────────────────── */}
      <Dialog
        open={rejectDialogOpen}
        onClose={() => {
          if (!rejectLoading) setRejectDialogOpen(false);
        }}
        maxWidth="xs"
        fullWidth
        data-testid="reject-dialog"
      >
        <DialogTitle>Reject Leave Request</DialogTitle>
        <DialogContent>
          <TextField
            label="Comment"
            placeholder="Provide a reason for rejection..."
            multiline
            rows={3}
            fullWidth
            required
            value={rejectComment}
            onChange={(e) => setRejectComment(e.target.value)}
            sx={{ mt: 1 }}
            data-testid="reject-comment-input"
          />
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => setRejectDialogOpen(false)}
            disabled={rejectLoading}
          >
            Cancel
          </Button>
          <Button
            variant="contained"
            color="error"
            onClick={() => void handleRejectSubmit()}
            disabled={!rejectComment.trim() || rejectLoading}
            data-testid="reject-submit-btn"
          >
            {rejectLoading ? (
              <CircularProgress size={18} color="inherit" />
            ) : (
              'Submit'
            )}
          </Button>
        </DialogActions>
      </Dialog>

      {/* ── Toast ─────────────────────────────────────────────────────────── */}
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
