/**
 * ApprovalsPage — Manager, HRAdmin, SuperAdmin
 *
 * Shows paginated list of pending leave requests awaiting the caller's approval.
 * Manager: only their direct reports. HRAdmin/SuperAdmin: all pending.
 *
 * Route: /approvals
 * Guard: RoleProtectedRoute allowedRoles=['Manager','HRAdmin','SuperAdmin']
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
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Pagination,
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
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import {
  getPendingApprovals,
  approveRequest,
  rejectRequest,
  type LeaveRequestDto,
} from '../api/approvalsApi';

const PAGE_SIZE = 10;

export default function ApprovalsPage() {
  const [requests, setRequests] = useState<LeaveRequestDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Reject dialog state
  const [rejectDialogOpen, setRejectDialogOpen] = useState(false);
  const [rejectTargetId, setRejectTargetId] = useState<string | null>(null);
  const [rejectComment, setRejectComment] = useState('');
  const [rejectError, setRejectError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);

  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const loadPage = useCallback(async (p: number) => {
    setLoading(true);
    setError(null);
    try {
      const result = await getPendingApprovals(p, PAGE_SIZE);
      setRequests(result.items);
      setTotal(result.total);
    } catch {
      setError('Failed to load pending approvals. Please refresh.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadPage(page);
  }, [page, loadPage]);

  const handleApprove = async (id: string) => {
    setActionLoading(true);
    try {
      await approveRequest(id);
      setSuccessMsg('Leave request approved.');
      void loadPage(page);
    } catch {
      setError('Failed to approve request.');
    } finally {
      setActionLoading(false);
    }
  };

  const openRejectDialog = (id: string) => {
    setRejectTargetId(id);
    setRejectComment('');
    setRejectError(null);
    setRejectDialogOpen(true);
  };

  const handleRejectConfirm = async () => {
    if (!rejectComment.trim()) {
      setRejectError('A comment is required when rejecting.');
      return;
    }
    if (!rejectTargetId) return;

    setActionLoading(true);
    try {
      await rejectRequest(rejectTargetId, rejectComment.trim());
      setRejectDialogOpen(false);
      setSuccessMsg('Leave request rejected.');
      void loadPage(page);
    } catch {
      setRejectError('Failed to reject request. Please try again.');
    } finally {
      setActionLoading(false);
    }
  };

  const pageCount = Math.ceil(total / PAGE_SIZE);

  return (
    <Box p={4}>
      <Typography variant="h4" mb={3}>
        Pending Approvals
      </Typography>

      {successMsg && (
        <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccessMsg(null)}>
          {successMsg}
        </Alert>
      )}
      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <Card variant="outlined">
        <CardContent>
          {loading ? (
            <Box display="flex" justifyContent="center" py={6}>
              <CircularProgress />
            </Box>
          ) : requests.length === 0 ? (
            <Typography color="text.secondary" textAlign="center" py={4}>
              No pending approvals.
            </Typography>
          ) : (
            <>
              <TableContainer component={Paper} elevation={0}>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Employee</TableCell>
                      <TableCell>Leave Type</TableCell>
                      <TableCell>Start Date</TableCell>
                      <TableCell>End Date</TableCell>
                      <TableCell align="center">Days</TableCell>
                      <TableCell>Retroactive</TableCell>
                      <TableCell>Reason</TableCell>
                      <TableCell align="center">Actions</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {requests.map(req => (
                      <TableRow key={req.id}>
                        <TableCell>{req.employeeName}</TableCell>
                        <TableCell>{req.leaveTypeName}</TableCell>
                        <TableCell>{req.startDate}</TableCell>
                        <TableCell>{req.endDate}</TableCell>
                        <TableCell align="center">{req.computedDays}</TableCell>
                        <TableCell>
                          {req.isRetroactive ? (
                            <Chip label="Retroactive" size="small" color="warning" />
                          ) : (
                            <Chip label="Normal" size="small" variant="outlined" />
                          )}
                        </TableCell>
                        <TableCell
                          sx={{
                            maxWidth: 200,
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            whiteSpace: 'nowrap',
                          }}
                          title={req.reason}
                        >
                          {req.reason}
                        </TableCell>
                        <TableCell align="center">
                          <Box display="flex" gap={1} justifyContent="center">
                            <Button
                              size="small"
                              variant="contained"
                              color="success"
                              startIcon={<CheckIcon />}
                              disabled={actionLoading}
                              onClick={() => void handleApprove(req.id)}
                              data-testid={`approve-btn-${req.id}`}
                            >
                              Approve
                            </Button>
                            <Button
                              size="small"
                              variant="outlined"
                              color="error"
                              startIcon={<CloseIcon />}
                              disabled={actionLoading}
                              onClick={() => openRejectDialog(req.id)}
                              data-testid={`reject-btn-${req.id}`}
                            >
                              Reject
                            </Button>
                          </Box>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>

              {pageCount > 1 && (
                <Box display="flex" justifyContent="center" mt={2}>
                  <Pagination
                    count={pageCount}
                    page={page}
                    onChange={(_, v) => setPage(v)}
                    color="primary"
                  />
                </Box>
              )}
            </>
          )}
        </CardContent>
      </Card>

      {/* Reject dialog */}
      <Dialog
        open={rejectDialogOpen}
        onClose={() => setRejectDialogOpen(false)}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle>Reject Leave Request</DialogTitle>
        <DialogContent>
          <DialogContentText mb={2}>
            Please provide a reason for rejection. This will be visible to the employee.
          </DialogContentText>
          <TextField
            label="Rejection Comment"
            multiline
            rows={3}
            fullWidth
            value={rejectComment}
            onChange={e => setRejectComment(e.target.value)}
            error={!!rejectError}
            helperText={rejectError}
            autoFocus
            inputProps={{ 'data-testid': 'reject-comment-input' }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRejectDialogOpen(false)} disabled={actionLoading}>
            Cancel
          </Button>
          <Button
            variant="contained"
            color="error"
            onClick={() => void handleRejectConfirm()}
            disabled={actionLoading}
            data-testid="confirm-reject-btn"
          >
            {actionLoading ? <CircularProgress size={18} color="inherit" /> : 'Reject'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
