import { useCallback, useEffect, useState } from 'react';
import {
  Box,
  Typography,
  TextField,
  Button,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Alert,
  CircularProgress,
  Grid,
  Paper,
} from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { DataGrid, type GridColDef, type GridRowParams } from '@mui/x-data-grid';
import { fetchAuditLogs, type AuditLog } from '../../api/auditApi';

// ── Helpers ───────────────────────────────────────────────────────────────────

/** Truncate a UUID to first 8 characters followed by an ellipsis. */
function truncateId(value: string): string {
  return value.length > 8 ? `${value.substring(0, 8)}...` : value;
}

/** Parse a raw JSON string and pretty-print it, or return as-is on error. */
function prettyJson(raw: string | null): string {
  if (raw === null || raw === undefined) return 'null';
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

// ── Filter state ──────────────────────────────────────────────────────────────

interface FilterState {
  entityType: string;
  actorId: string;
  from: string;
  to: string;
}

const EMPTY_FILTERS: FilterState = {
  entityType: '',
  actorId: '',
  from: '',
  to: '',
};

// ── Column definitions ────────────────────────────────────────────────────────

const COLUMNS: GridColDef[] = [
  {
    field: 'action',
    headerName: 'Action',
    width: 160,
    sortable: false,
  },
  {
    field: 'entityType',
    headerName: 'Entity Type',
    width: 160,
    sortable: false,
  },
  {
    field: 'entityId',
    headerName: 'Entity ID',
    width: 130,
    sortable: false,
    valueFormatter: ({ value }: { value: unknown }) =>
      typeof value === 'string' ? truncateId(value) : String(value),
  },
  {
    field: 'actorId',
    headerName: 'Actor',
    width: 130,
    sortable: false,
    valueFormatter: ({ value }: { value: unknown }) =>
      typeof value === 'string' ? truncateId(value) : String(value),
  },
  {
    field: 'createdAt',
    headerName: 'Timestamp',
    width: 200,
    sortable: false,
    valueFormatter: ({ value }: { value: unknown }) =>
      typeof value === 'string'
        ? new Date(value).toLocaleString()
        : String(value),
  },
];

// ── Component ─────────────────────────────────────────────────────────────────

/**
 * REPORTING-UI-005 — Audit Trail Admin Page.
 * Route: /admin/audit  (HRAdmin and SuperAdmin only via RoleProtectedRoute).
 *
 * Features:
 * - MUI DataGrid v5 with server-side pagination.
 * - Filter panel: entity_type, actor_id, date range from/to.
 * - Row-click detail panel: MUI Accordion with JSON diff (old_value vs new_value).
 * - Entity ID and Actor ID truncated to 8 chars + ellipsis in the grid.
 * - NO delete button for any role (audit log is immutable).
 */
export default function AuditTrailPage() {
  // ── Filter form state (unsubmitted edits) ──────────────────────────────
  const [draftFilters, setDraftFilters] = useState<FilterState>(EMPTY_FILTERS);

  // ── Applied filters (triggers data fetch when changed) ─────────────────
  const [appliedFilters, setAppliedFilters] =
    useState<FilterState>(EMPTY_FILTERS);

  // ── Pagination (page is 1-indexed to match the API) ───────────────────
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  // ── Data state ─────────────────────────────────────────────────────────
  const [rows, setRows] = useState<AuditLog[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // ── Row detail accordion ───────────────────────────────────────────────
  const [selectedRow, setSelectedRow] = useState<AuditLog | null>(null);

  // ── Fetch data ─────────────────────────────────────────────────────────
  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetchAuditLogs({
        ...(appliedFilters.entityType && { entity_type: appliedFilters.entityType }),
        ...(appliedFilters.actorId   && { actor_id:    appliedFilters.actorId }),
        ...(appliedFilters.from      && { from:         appliedFilters.from }),
        ...(appliedFilters.to        && { to:           appliedFilters.to }),
        page,
        limit: pageSize,
      });
      setRows(response.data);
      setTotal(response.total);
    } catch {
      setError('Failed to load audit logs. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [appliedFilters, page, pageSize]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  // ── Handlers ───────────────────────────────────────────────────────────

  function handleApplyFilters() {
    setPage(1);            // reset to first page on new filter
    setSelectedRow(null);  // close detail panel
    setAppliedFilters({ ...draftFilters });
  }

  function handleClearFilters() {
    setDraftFilters(EMPTY_FILTERS);
    setPage(1);
    setSelectedRow(null);
    setAppliedFilters(EMPTY_FILTERS);
  }

  function handleRowClick(params: GridRowParams) {
    const clickedRow = params.row as AuditLog;
    setSelectedRow((prev) =>
      prev?.id === clickedRow.id ? null : clickedRow,
    );
  }

  // ── Render ─────────────────────────────────────────────────────────────
  return (
    <Box p={3}>
      <Typography variant="h5" fontWeight={600} mb={2}>
        Audit Trail
      </Typography>

      {/* ── Filter Panel ── */}
      <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
        <Typography variant="subtitle2" mb={1} color="text.secondary">
          Filters
        </Typography>
        <Grid container spacing={2} alignItems="center">
          <Grid item xs={12} sm={3}>
            <TextField
              label="Entity Type"
              size="small"
              fullWidth
              value={draftFilters.entityType}
              onChange={(e) =>
                setDraftFilters((f) => ({ ...f, entityType: e.target.value }))
              }
              placeholder="e.g. User, LeaveRequest"
            />
          </Grid>
          <Grid item xs={12} sm={3}>
            <TextField
              label="Actor ID"
              size="small"
              fullWidth
              value={draftFilters.actorId}
              onChange={(e) =>
                setDraftFilters((f) => ({ ...f, actorId: e.target.value }))
              }
              placeholder="UUID of the actor"
            />
          </Grid>
          <Grid item xs={12} sm={2}>
            <TextField
              label="From"
              type="date"
              size="small"
              fullWidth
              value={draftFilters.from}
              onChange={(e) =>
                setDraftFilters((f) => ({ ...f, from: e.target.value }))
              }
              InputLabelProps={{ shrink: true }}
            />
          </Grid>
          <Grid item xs={12} sm={2}>
            <TextField
              label="To"
              type="date"
              size="small"
              fullWidth
              value={draftFilters.to}
              onChange={(e) =>
                setDraftFilters((f) => ({ ...f, to: e.target.value }))
              }
              InputLabelProps={{ shrink: true }}
            />
          </Grid>
          <Grid item xs={12} sm={2}>
            <Box display="flex" gap={1}>
              <Button
                variant="contained"
                size="small"
                onClick={handleApplyFilters}
              >
                Apply
              </Button>
              <Button
                variant="outlined"
                size="small"
                onClick={handleClearFilters}
              >
                Clear
              </Button>
            </Box>
          </Grid>
        </Grid>
      </Paper>

      {/* ── Error banner ── */}
      {error !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {/* ── Data Grid ── */}
      <Box sx={{ height: 480 }}>
        <DataGrid
          rows={rows}
          columns={COLUMNS}
          getRowId={(row: AuditLog) => row.id}
          // Server-side pagination
          paginationMode="server"
          rowCount={total}
          page={page - 1}          // DataGrid v5 is 0-indexed
          pageSize={pageSize}
          onPageChange={(newPage: number) => {
            setPage(newPage + 1);
            setSelectedRow(null);
          }}
          onPageSizeChange={(newSize: number) => {
            setPageSize(newSize);
            setPage(1);
            setSelectedRow(null);
          }}
          rowsPerPageOptions={[10, 20, 50, 100]}
          loading={loading}
          onRowClick={handleRowClick}
          // Highlight selected row
          selectionModel={
            selectedRow !== null ? [selectedRow.id] : []
          }
          disableSelectionOnClick={false}
          disableColumnFilter
          disableColumnMenu
          components={{
            LoadingOverlay: () => (
              <Box
                display="flex"
                alignItems="center"
                justifyContent="center"
                height="100%"
              >
                <CircularProgress size={32} />
              </Box>
            ),
          }}
          sx={{
            '& .MuiDataGrid-row': { cursor: 'pointer' },
          }}
        />
      </Box>

      {/* ── Row Detail Accordion ── */}
      {selectedRow !== null && (
        <Accordion
          expanded
          sx={{ mt: 2 }}
          onChange={() => setSelectedRow(null)}
        >
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography fontWeight={500}>
              Detail — {selectedRow.action} on {selectedRow.entityType}{' '}
              <Typography
                component="span"
                color="text.secondary"
                fontSize="0.875rem"
              >
                (ID: {truncateId(selectedRow.entityId)})
              </Typography>
            </Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Grid container spacing={2}>
              {/* Before (old_value) */}
              <Grid item xs={12} md={6}>
                <Typography
                  variant="subtitle2"
                  color="text.secondary"
                  mb={0.5}
                >
                  Before
                </Typography>
                <Box
                  component="pre"
                  sx={{
                    m: 0,
                    p: 1.5,
                    bgcolor: 'grey.100',
                    borderRadius: 1,
                    fontSize: '0.75rem',
                    fontFamily: 'monospace',
                    overflowX: 'auto',
                    whiteSpace: 'pre-wrap',
                    wordBreak: 'break-all',
                    maxHeight: 300,
                    overflowY: 'auto',
                  }}
                >
                  {prettyJson(selectedRow.oldValue)}
                </Box>
              </Grid>
              {/* After (new_value) */}
              <Grid item xs={12} md={6}>
                <Typography
                  variant="subtitle2"
                  color="text.secondary"
                  mb={0.5}
                >
                  After
                </Typography>
                <Box
                  component="pre"
                  sx={{
                    m: 0,
                    p: 1.5,
                    bgcolor: 'grey.100',
                    borderRadius: 1,
                    fontSize: '0.75rem',
                    fontFamily: 'monospace',
                    overflowX: 'auto',
                    whiteSpace: 'pre-wrap',
                    wordBreak: 'break-all',
                    maxHeight: 300,
                    overflowY: 'auto',
                  }}
                >
                  {prettyJson(selectedRow.newValue)}
                </Box>
              </Grid>
            </Grid>
          </AccordionDetails>
        </Accordion>
      )}
    </Box>
  );
}
