import React, { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
  Typography,
} from '@mui/material';
import { useSelector } from 'react-redux';
import type { RootState } from '../../store';
import {
  fetchMyCompOffRequests,
  approveCompOffRequest,
  rejectCompOffRequest,
  type CompOffRequestDto,
  type CompOffRequestStatus,
} from '../../api/compOffApi';
import {
  fetchMyCompOffCredits,
  type CompOffCredit,
} from '../../api/leaveBalanceApi';

/** Roles permitted to approve / reject team comp-off requests. */
const APPROVER_ROLES = new Set<string>(['Manager', 'HRAdmin', 'SuperAdmin']);

function statusColor(
  status: CompOffRequestStatus,
): 'warning' | 'success' | 'error' | 'default' {
  switch (status) {
    case 'Pending':  return 'warning';
    case 'Approved': return 'success';
    case 'Rejected': return 'error';
    default:         return 'default';
  }
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-IN', {
    day: '2-digit', month: 'short', year: 'numeric',
  });
}

export default function CompOffHistoryPage(): React.JSX.Element {
  const role       = useSelector((state: RootState) => state.auth.role);
  const isApprover = role !== null && APPROVER_ROLES.has(role);

  const [tab, setTab]                 = useState(0);
  const [requests, setRequests]       = useState<CompOffRequestDto[]>([]);
  const [credits, setCredits]         = useState<CompOffCredit[]>([]);
  const [loadingData, setLoadingData] = useState(true);
  const [loadError, setLoadError]     = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actioning, setActioning]     = useState<string | null>(null);

  const loadData = useCallback(async (): Promise<void> => {
    setLoadingData(true);
    setLoadError(null);
    try {
      const [reqs, creds] = await Promise.all([
        fetchMyCompOffRequests(),
        fetchMyCompOffCredits(),
      ]);
      setRequests(reqs);
      setCredits(creds);
    } catch (err: unknown) {
      setLoadError(
        err instanceof Error ? err.message : 'Failed to load comp-off data.',
      );
    } finally {
      setLoadingData(false);
    }
  }, []);

  useEffect(() => { void loadData(); }, [loadData]);

  async function handleApprove(id: string): Promise<void> {
    setActioning(id);
    setActionError(null);
    try {
      await approveCompOffRequest(id);
      await loadData();
    } catch (err: unknown) {
      setActionError(
        err instanceof Error ? err.message : 'Failed to approve request.',
      );
    } finally {
      setActioning(null);
    }
  }

  async function handleReject(id: string): Promise<void> {
    setActioning(id);
    setActionError(null);
    try {
      await rejectCompOffRequest(id);
      await loadData();
    } catch (err: unknown) {
      setActionError(
        err instanceof Error ? err.message : 'Failed to reject request.',
      );
    } finally {
      setActioning(null);
    }
  }

  // Pending requests from the user's own list (manager approval view).
  // A dedicated team endpoint (COMPOFF-API-002) will replace this when available.
  const pendingRequests = requests.filter((r) => r.status === 'Pending');

  return (
    <Box maxWidth={960} mx="auto" mt={4} px={2}>
      <Typography variant="h5" fontWeight={600} gutterBottom>
        Comp-Off History
      </Typography>

      {loadError !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {loadError}
        </Alert>
      )}
      {actionError !== null && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setActionError(null)}>
          {actionError}
        </Alert>
      )}

      {loadingData ? (
        <Box display="flex" justifyContent="center" mt={6}>
          <CircularProgress />
        </Box>
      ) : (
        <>
          <Tabs
            value={tab}
            onChange={(_, v: number) => setTab(v)}
            sx={{ mb: 2 }}
          >
            <Tab label={`My Requests (${requests.length})`} />
            <Tab label={`My Credits (${credits.length})`} />
          </Tabs>

          {/* Tab 0 — My Requests */}
          {tab === 0 && (
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Date Worked</TableCell>
                    <TableCell>Hours</TableCell>
                    <TableCell>Credit Days</TableCell>
                    <TableCell>Reason</TableCell>
                    <TableCell>Status</TableCell>
                    <TableCell>Submitted</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {requests.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={6} align="center" sx={{ py: 4 }}>
                        <Typography variant="body2" color="text.secondary">
                          No comp-off requests found.
                        </Typography>
                      </TableCell>
                    </TableRow>
                  ) : (
                    requests.map((r) => (
                      <TableRow key={r.id} hover>
                        <TableCell>{formatDate(r.workedDate)}</TableCell>
                        <TableCell>{r.hoursWorked} h</TableCell>
                        <TableCell>{r.creditDays} d</TableCell>
                        <TableCell
                          sx={{
                            maxWidth: 220,
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            whiteSpace: 'nowrap',
                          }}
                          title={r.reason}
                        >
                          {r.reason}
                        </TableCell>
                        <TableCell>
                          <Chip
                            label={r.status}
                            color={statusColor(r.status)}
                            size="small"
                          />
                        </TableCell>
                        <TableCell>{formatDate(r.createdAt)}</TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          )}

          {/* Tab 1 — My Credits */}
          {tab === 1 && (
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Credit Days</TableCell>
                    <TableCell>Used Days</TableCell>
                    <TableCell>Remaining</TableCell>
                    <TableCell>Expires</TableCell>
                    <TableCell>Credited On</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {credits.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={5} align="center" sx={{ py: 4 }}>
                        <Typography variant="body2" color="text.secondary">
                          No comp-off credits found.
                        </Typography>
                      </TableCell>
                    </TableRow>
                  ) : (
                    credits.map((c) => {
                      const remaining = c.creditDays - c.usedDays;
                      const isExpired = new Date(c.expiresAt) < new Date();
                      return (
                        <TableRow key={c.id} hover>
                          <TableCell>{c.creditDays} d</TableCell>
                          <TableCell>{c.usedDays} d</TableCell>
                          <TableCell>
                            <Typography
                              variant="body2"
                              color={remaining <= 0 ? 'text.disabled' : 'success.main'}
                              fontWeight={500}
                            >
                              {remaining} d
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <Chip
                              label={formatDate(c.expiresAt)}
                              color={isExpired ? 'error' : 'default'}
                              size="small"
                              variant={isExpired ? 'filled' : 'outlined'}
                            />
                          </TableCell>
                          <TableCell>{formatDate(c.createdAt)}</TableCell>
                        </TableRow>
                      );
                    })
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          )}

          {/* Manager / HRAdmin section — team pending approvals */}
          {isApprover && (
            <>
              <Divider sx={{ my: 4 }} />
              <Typography variant="h6" fontWeight={600} gutterBottom>
                Team Pending Requests
              </Typography>
              <Typography variant="body2" color="text.secondary" mb={2}>
                Approve or reject pending comp-off requests from your team.
              </Typography>

              {pendingRequests.length === 0 ? (
                <Alert severity="info">
                  No pending requests awaiting your action.
                </Alert>
              ) : (
                <TableContainer component={Paper} variant="outlined">
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Employee</TableCell>
                        <TableCell>Date Worked</TableCell>
                        <TableCell>Hours</TableCell>
                        <TableCell>Credit</TableCell>
                        <TableCell>Reason</TableCell>
                        <TableCell>Submitted</TableCell>
                        <TableCell align="center">Actions</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {pendingRequests.map((r) => (
                        <TableRow key={r.id} hover>
                          <TableCell>
                            {r.employeeName ?? r.employeeId.slice(0, 8)}
                          </TableCell>
                          <TableCell>{formatDate(r.workedDate)}</TableCell>
                          <TableCell>{r.hoursWorked} h</TableCell>
                          <TableCell>{r.creditDays} d</TableCell>
                          <TableCell
                            sx={{
                              maxWidth: 200,
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                              whiteSpace: 'nowrap',
                            }}
                            title={r.reason}
                          >
                            {r.reason}
                          </TableCell>
                          <TableCell>{formatDate(r.createdAt)}</TableCell>
                          <TableCell align="center">
                            <Stack direction="row" spacing={1} justifyContent="center">
                              <Button
                                size="small"
                                variant="contained"
                                color="success"
                                disabled={actioning === r.id}
                                onClick={() => { void handleApprove(r.id); }}
                                startIcon={
                                  actioning === r.id
                                    ? <CircularProgress size={12} color="inherit" />
                                    : null
                                }
                              >
                                Approve
                              </Button>
                              <Button
                                size="small"
                                variant="outlined"
                                color="error"
                                disabled={actioning === r.id}
                                onClick={() => { void handleReject(r.id); }}
                              >
                                Reject
                              </Button>
                            </Stack>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              )}
            </>
          )}
        </>
      )}
    </Box>
  );
}
