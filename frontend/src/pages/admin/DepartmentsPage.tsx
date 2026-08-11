import React, { useCallback, useEffect, useState } from 'react';
import { Box, Button, Chip, CircularProgress, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, IconButton, TextField, Typography, Snackbar, Alert } from '@mui/material';
import { DataGrid, type GridColDef, type GridRenderCellParams } from '@mui/x-data-grid';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import { useForm, type SubmitHandler } from 'react-hook-form';
import { fetchDepartments, createDepartment, updateDepartment, deleteDepartment, type Department } from '../../api/departmentsApi';
import type { AxiosError } from 'axios';

interface DeptFormValues { name: string; description: string; }
interface ApiErrorBody { error?: { message?: string }; }

const columns = (onEdit: (d: Department) => void, onDelete: (d: Department) => void): GridColDef[] => [
  { field: 'name', headerName: 'Name', flex: 1, minWidth: 160 },
  { field: 'description', headerName: 'Description', flex: 2, minWidth: 200, valueGetter: (p: GridRenderCellParams) => (p.value as string | null) ?? '—' },
  { field: 'isActive', headerName: 'Status', width: 120, renderCell: (p: GridRenderCellParams) => (p.value as boolean) ? <Chip label="Active" color="success" size="small" /> : <Chip label="Inactive" size="small" sx={{ opacity: 0.6 }} /> },
  { field: 'actions', headerName: 'Actions', width: 120, sortable: false, renderCell: (p: GridRenderCellParams) => {
    const row = p.row as Department;
    return <Box><IconButton size="small" onClick={() => onEdit(row)}><EditIcon fontSize="small" /></IconButton><IconButton size="small" color="error" onClick={() => onDelete(row)}><DeleteIcon fontSize="small" /></IconButton></Box>;
  }},
];

export default function DepartmentsPage() {
  const [departments, setDepartments] = useState<Department[]>([]);
  const [loading, setLoading] = useState(true);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<Department | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Department | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [snackbar, setSnackbar] = useState<{ open: boolean; message: string; severity: 'success' | 'error' }>({ open: false, message: '', severity: 'success' });
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<DeptFormValues>();

  const loadDepartments = useCallback(async () => {
    setLoading(true);
    try { const data = await fetchDepartments(1, 200); setDepartments(data.items); }
    catch { setSnackbar({ open: true, message: 'Failed to load departments.', severity: 'error' }); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { void loadDepartments(); }, [loadDepartments]);

  const onSubmit: SubmitHandler<DeptFormValues> = async values => {
    try {
      if (editTarget) { await updateDepartment(editTarget.id, { name: values.name, description: values.description || null }); setSnackbar({ open: true, message: 'Department updated.', severity: 'success' }); }
      else { await createDepartment({ name: values.name, description: values.description || null }); setSnackbar({ open: true, message: 'Department created.', severity: 'success' }); }
      setDialogOpen(false); await loadDepartments();
    } catch (err) {
      const msg = (err as AxiosError<ApiErrorBody>).response?.data?.error?.message ?? 'Failed to save department.';
      setSnackbar({ open: true, message: msg, severity: 'error' });
    }
  };

  const handleDeleteConfirm = async () => {
    if (!deleteTarget) return;
    setDeleteLoading(true);
    try { await deleteDepartment(deleteTarget.id); setSnackbar({ open: true, message: 'Department deactivated.', severity: 'success' }); setDeleteDialogOpen(false); setDeleteTarget(null); await loadDepartments(); }
    catch (err) { setSnackbar({ open: true, message: (err as AxiosError).response?.status === 409 ? 'Cannot delete — employees assigned' : 'Failed to delete department.', severity: 'error' }); setDeleteDialogOpen(false); }
    finally { setDeleteLoading(false); }
  };

  if (loading && departments.length === 0) return <Box display="flex" justifyContent="center" alignItems="center" minHeight={300}><CircularProgress /></Box>;

  return (
    <Box p={3}>
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={2}>
        <Typography variant="h5" fontWeight={600}>Departments</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => { setEditTarget(null); reset({ name: '', description: '' }); setDialogOpen(true); }}>Add Department</Button>
      </Box>
      <Box sx={{ height: 520 }}>
        <DataGrid rows={departments} columns={columns(dept => { setEditTarget(dept); reset({ name: dept.name, description: dept.description ?? '' }); setDialogOpen(true); }, dept => { setDeleteTarget(dept); setDeleteDialogOpen(true); })}
          getRowId={(r: Department) => r.id} pageSize={20} rowsPerPageOptions={[10, 20, 50]} disableSelectionOnClick loading={loading}
          getRowClassName={p => (p.row as Department).isActive ? '' : 'row-inactive'}
          sx={{ '& .row-inactive': { opacity: 0.55, backgroundColor: 'action.hover' } }} />
      </Box>
      <Dialog open={dialogOpen} onClose={() => { if (!isSubmitting) setDialogOpen(false); }} maxWidth="sm" fullWidth>
        <form onSubmit={e => { void handleSubmit(onSubmit)(e); }}>
          <DialogTitle>{editTarget ? 'Edit Department' : 'Add Department'}</DialogTitle>
          <DialogContent>
            <TextField label="Name" fullWidth margin="normal" {...register('name', { required: 'Department name is required.' })} error={!!errors.name} helperText={errors.name?.message} autoFocus />
            <TextField label="Description (optional)" fullWidth margin="normal" multiline rows={3} {...register('description')} />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setDialogOpen(false)} disabled={isSubmitting}>Cancel</Button>
            <Button type="submit" variant="contained" disabled={isSubmitting}>{editTarget ? 'Save Changes' : 'Create'}</Button>
          </DialogActions>
        </form>
      </Dialog>
      <Dialog open={deleteDialogOpen} onClose={() => { if (!deleteLoading) { setDeleteDialogOpen(false); setDeleteTarget(null); } }} maxWidth="xs" fullWidth>
        <DialogTitle>Deactivate Department</DialogTitle>
        <DialogContent><DialogContentText>Deactivate <strong>{deleteTarget?.name ?? ''}</strong>?</DialogContentText></DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteDialogOpen(false)} disabled={deleteLoading}>Cancel</Button>
          <Button color="error" variant="contained" disabled={deleteLoading} onClick={() => void handleDeleteConfirm()}>{deleteLoading ? 'Deleting…' : 'Deactivate'}</Button>
        </DialogActions>
      </Dialog>
      <Snackbar open={snackbar.open} autoHideDuration={5000} onClose={() => setSnackbar(s => ({ ...s, open: false }))} anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}>
        <Alert onClose={() => setSnackbar(s => ({ ...s, open: false }))} severity={snackbar.severity} sx={{ width: '100%' }}>{snackbar.message}</Alert>
      </Snackbar>
    </Box>
  );
}
