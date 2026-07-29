import { useEffect, useState, useCallback } from 'react';
import { useSelector } from 'react-redux';
import { Navigate } from 'react-router-dom';
import {
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  MenuItem,
  Select,
  Snackbar,
  Alert,
  Switch,
  TextField,
  Typography,
  IconButton,
  Tooltip,
} from '@mui/material';
import { DataGrid, type GridColDef, type GridRenderCellParams } from '@mui/x-data-grid';
import DeleteIcon from '@mui/icons-material/Delete';
import EditIcon from '@mui/icons-material/Edit';
import AddIcon from '@mui/icons-material/Add';
import { useForm, Controller } from 'react-hook-form';
import type { RootState } from '../../store';
import {
  fetchLeaveTypes,
  createLeaveType,
  updateLeaveType,
  deleteLeaveType,
  ACCRUAL_TYPE_LABELS,
  type LeaveTypeDto,
  type CreateLeaveTypePayload,
} from '../../api/leaveTypesApi';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface FormValues {
  name: string;
  accrualType: number;
  maxDaysPerYear: string; // kept as string for the number input
  requiresDocument: boolean;
}

const ACCRUAL_OPTIONS = [
  { value: 0, label: 'Annual' },
  { value: 1, label: 'OneTime' },
  { value: 2, label: 'Unlimited' },
];

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export default function LeaveTypesPage() {
  const auth = useSelector((state: RootState) => state.auth);

  // Only HR Admin may access this page
  if (!auth.isAuthenticated || auth.user?.role !== 'HRAdmin') {
    return <Navigate to="/dashboard" replace />;
  }

  const token = auth.accessToken ?? '';

  const [rows, setRows] = useState<LeaveTypeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<LeaveTypeDto | null>(null);
  const [toast, setToast] = useState<{ open: boolean; message: string; severity: 'success' | 'error' }>(
    { open: false, message: '', severity: 'success' },
  );

  const { control, handleSubmit, reset, watch, setValue } = useForm<FormValues>({
    defaultValues: {
      name: '',
      accrualType: 0,
      maxDaysPerYear: '',
      requiresDocument: false,
    },
  });

  const accrualTypeValue = watch('accrualType');

  // When accrualType switches to Unlimited, clear and disable maxDaysPerYear
  useEffect(() => {
    if (accrualTypeValue === 2) {
      setValue('maxDaysPerYear', '');
    }
  }, [accrualTypeValue, setValue]);

  // ---------------------------------------------------------------------------
  // Data fetching
  // ---------------------------------------------------------------------------

  const loadRows = useCallback(async () => {
    setLoading(true);
    try {
      const data = await fetchLeaveTypes(token);
      setRows(data);
    } catch {
      showToast('Failed to load leave types', 'error');
    } finally {
      setLoading(false);
    }
  }, [token]);

  useEffect(() => {
    void loadRows();
  }, [loadRows]);

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  function showToast(message: string, severity: 'success' | 'error') {
    setToast({ open: true, message, severity });
  }

  function openAddDialog() {
    setEditing(null);
    reset({ name: '', accrualType: 0, maxDaysPerYear: '', requiresDocument: false });
    setDialogOpen(true);
  }

  function openEditDialog(row: LeaveTypeDto) {
    setEditing(row);
    reset({
      name: row.name,
      accrualType: row.accrualType,
      maxDaysPerYear: row.maxDaysPerYear != null ? String(row.maxDaysPerYear) : '',
      requiresDocument: row.requiresDocument,
    });
    setDialogOpen(true);
  }

  function closeDialog() {
    setDialogOpen(false);
    setEditing(null);
  }

  // ---------------------------------------------------------------------------
  // Submit
  // ---------------------------------------------------------------------------

  async function onSubmit(values: FormValues) {
    const payload: CreateLeaveTypePayload = {
      name: values.name.trim(),
      accrualType: values.accrualType,
      maxDaysPerYear:
        values.accrualType === 2 || values.maxDaysPerYear === ''
          ? null
          : Number(values.maxDaysPerYear),
      requiresDocument: values.requiresDocument,
    };

    try {
      if (editing) {
        await updateLeaveType(token, editing.id, payload);
        showToast('Leave type updated', 'success');
      } else {
        await createLeaveType(token, payload);
        showToast('Leave type created', 'success');
      }
      closeDialog();
      void loadRows();
    } catch {
      showToast('Failed to save leave type', 'error');
    }
  }

  // ---------------------------------------------------------------------------
  // Delete
  // ---------------------------------------------------------------------------

  async function handleDelete(id: string) {
    try {
      const deleted = await deleteLeaveType(token, id);
      if (!deleted) {
        showToast('Cannot delete — leave balances exist', 'error');
        return;
      }
      showToast('Leave type deleted', 'success');
      void loadRows();
    } catch {
      showToast('Failed to delete leave type', 'error');
    }
  }

  // ---------------------------------------------------------------------------
  // Grid columns
  // ---------------------------------------------------------------------------

  const columns: GridColDef<LeaveTypeDto>[] = [
    { field: 'name', headerName: 'Name', flex: 1.5, minWidth: 160 },
    {
      field: 'maxDaysPerYear',
      headerName: 'Max Days/Year',
      flex: 1,
      minWidth: 130,
      renderCell: (params: GridRenderCellParams<LeaveTypeDto>) =>
        params.value != null ? String(params.value) : <em style={{ color: '#888' }}>Unlimited</em>,
    },
    {
      field: 'accrualType',
      headerName: 'Accrual Type',
      flex: 1,
      minWidth: 120,
      renderCell: (params: GridRenderCellParams<LeaveTypeDto>) => (
        <Chip
          label={ACCRUAL_TYPE_LABELS[params.value as number] ?? 'Unknown'}
          size="small"
          variant="outlined"
        />
      ),
    },
    {
      field: 'requiresDocument',
      headerName: 'Doc Required',
      flex: 0.8,
      minWidth: 120,
      renderCell: (params: GridRenderCellParams<LeaveTypeDto>) =>
        params.value ? (
          <Chip label="Yes" color="warning" size="small" />
        ) : (
          <Chip label="No" size="small" />
        ),
    },
    {
      field: 'isActive',
      headerName: 'Status',
      flex: 0.8,
      minWidth: 100,
      renderCell: (params: GridRenderCellParams<LeaveTypeDto>) =>
        params.value ? (
          <Chip label="Active" color="success" size="small" />
        ) : (
          <Chip label="Inactive" color="default" size="small" sx={{ opacity: 0.6 }} />
        ),
    },
    {
      field: 'actions',
      headerName: 'Actions',
      sortable: false,
      flex: 0.8,
      minWidth: 100,
      renderCell: (params: GridRenderCellParams<LeaveTypeDto>) => (
        <Box>
          <Tooltip title="Edit">
            <IconButton size="small" onClick={() => openEditDialog(params.row)}>
              <EditIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Delete">
            <IconButton
              size="small"
              color="error"
              onClick={() => void handleDelete(params.row.id)}
            >
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Box>
      ),
    },
  ];

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------

  return (
    <Box p={3}>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={2}>
        <Typography variant="h5">Leave Types</Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={openAddDialog}
        >
          Add Leave Type
        </Button>
      </Box>

      {loading ? (
        <Box display="flex" justifyContent="center" mt={6}>
          <CircularProgress />
        </Box>
      ) : (
        <DataGrid
          rows={rows}
          columns={columns}
          getRowId={(row) => row.id}
          autoHeight
          pageSizeOptions={[20, 50, 100]}
          disableRowSelectionOnClick
          getRowClassName={(params) =>
            !params.row.isActive ? 'row--inactive' : ''
          }
          sx={{
            '& .row--inactive': {
              opacity: 0.55,
            },
          }}
        />
      )}

      {/* Add / Edit Dialog */}
      <Dialog open={dialogOpen} onClose={closeDialog} maxWidth="sm" fullWidth>
        <DialogTitle>{editing ? 'Edit Leave Type' : 'Add Leave Type'}</DialogTitle>
        {/* eslint-disable-next-line @typescript-eslint/no-misused-promises */}
        <form onSubmit={handleSubmit(onSubmit)}>
          <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
            {/* Name */}
            <Controller
              name="name"
              control={control}
              rules={{ required: 'Name is required' }}
              render={({ field, fieldState }) => (
                <TextField
                  {...field}
                  label="Name"
                  required
                  fullWidth
                  error={!!fieldState.error}
                  helperText={fieldState.error?.message}
                />
              )}
            />

            {/* Accrual Type */}
            <Controller
              name="accrualType"
              control={control}
              render={({ field }) => (
                <TextField
                  {...field}
                  select
                  label="Accrual Type"
                  fullWidth
                  SelectProps={{ native: false }}
                >
                  {ACCRUAL_OPTIONS.map((opt) => (
                    <MenuItem key={opt.value} value={opt.value}>
                      {opt.label}
                    </MenuItem>
                  ))}
                </TextField>
              )}
            />

            {/* Max Days Per Year — disabled when Unlimited */}
            <Controller
              name="maxDaysPerYear"
              control={control}
              render={({ field }) => (
                <TextField
                  {...field}
                  label="Max Days Per Year"
                  type="number"
                  fullWidth
                  disabled={accrualTypeValue === 2}
                  inputProps={{ min: 0, step: 0.5 }}
                  helperText={
                    accrualTypeValue === 2
                      ? 'Not applicable for Unlimited accrual'
                      : 'Leave blank for unlimited'
                  }
                />
              )}
            />

            {/* Requires Document */}
            <Controller
              name="requiresDocument"
              control={control}
              render={({ field }) => (
                <FormControlLabel
                  control={
                    <Switch
                      checked={field.value}
                      onChange={(e) => field.onChange(e.target.checked)}
                    />
                  }
                  label="Requires supporting document"
                />
              )}
            />
          </DialogContent>

          <DialogActions>
            <Button onClick={closeDialog}>Cancel</Button>
            <Button type="submit" variant="contained">
              {editing ? 'Save' : 'Create'}
            </Button>
          </DialogActions>
        </form>
      </Dialog>

      {/* Toast notifications */}
      <Snackbar
        open={toast.open}
        autoHideDuration={4000}
        onClose={() => setToast((t) => ({ ...t, open: false }))}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity={toast.severity} variant="filled">
          {toast.message}
        </Alert>
      </Snackbar>
    </Box>
  );
}
