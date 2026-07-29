import React, { useCallback, useEffect, useState } from 'react'
import {
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  IconButton,
  TextField,
  Typography,
  Snackbar,
  Alert,
} from '@mui/material'
import { DataGrid, type GridColDef, type GridRenderCellParams } from '@mui/x-data-grid'
import AddIcon from '@mui/icons-material/Add'
import EditIcon from '@mui/icons-material/Edit'
import DeleteIcon from '@mui/icons-material/Delete'
import { useForm, type SubmitHandler } from 'react-hook-form'
import { type Department, departmentsApi } from '../../api/departmentsApi'
import type { AxiosError } from 'axios'

// ------------------------------------------------------------------
// Types
// ------------------------------------------------------------------

interface DeptFormValues {
  name: string
  description: string
}

interface ApiErrorBody {
  error?: { message?: string }
}

interface SnackbarState {
  open: boolean
  message: string
  severity: 'success' | 'error'
}

// ------------------------------------------------------------------
// Component
// ------------------------------------------------------------------

/**
 * /admin/departments — HR Admin department management page.
 * Requires HRAdmin or SuperAdmin role (enforced by RoleProtectedRoute in App).
 * FR-21 to FR-26.
 */
function DepartmentsPage(): JSX.Element {
  const [departments, setDepartments] = useState<Department[]>([])
  const [loading, setLoading] = useState(true)

  // Dialog state
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<Department | null>(null)

  // Delete confirmation state
  const [deleteTarget, setDeleteTarget] = useState<Department | null>(null)
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false)
  const [deleteLoading, setDeleteLoading] = useState(false)

  // Snackbar
  const [snackbar, setSnackbar] = useState<SnackbarState>({
    open: false,
    message: '',
    severity: 'success',
  })

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<DeptFormValues>()

  // ------------------------------------------------------------------
  // Data fetching
  // ------------------------------------------------------------------

  const loadDepartments = useCallback(async () => {
    setLoading(true)
    try {
      const data = await departmentsApi.getAll(true)
      setDepartments(data)
    } catch {
      showSnackbar('Failed to load departments.', 'error')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadDepartments()
  }, [loadDepartments])

  // ------------------------------------------------------------------
  // Snackbar helper
  // ------------------------------------------------------------------

  const showSnackbar = (message: string, severity: 'success' | 'error') => {
    setSnackbar({ open: true, message, severity })
  }

  const handleSnackbarClose = () => {
    setSnackbar((prev) => ({ ...prev, open: false }))
  }

  // ------------------------------------------------------------------
  // Add / Edit dialog
  // ------------------------------------------------------------------

  const handleAddClick = () => {
    setEditTarget(null)
    reset({ name: '', description: '' })
    setDialogOpen(true)
  }

  const handleEditClick = (dept: Department) => {
    setEditTarget(dept)
    reset({ name: dept.name, description: dept.description ?? '' })
    setDialogOpen(true)
  }

  const handleDialogClose = () => {
    if (!isSubmitting) setDialogOpen(false)
  }

  const onSubmit: SubmitHandler<DeptFormValues> = async (values) => {
    try {
      if (editTarget !== null) {
        await departmentsApi.update(editTarget.id, {
          name: values.name,
          description: values.description || undefined,
        })
        showSnackbar('Department updated.', 'success')
      } else {
        await departmentsApi.create({
          name: values.name,
          description: values.description || undefined,
        })
        showSnackbar('Department created.', 'success')
      }
      setDialogOpen(false)
      await loadDepartments()
    } catch (err) {
      const axiosErr = err as AxiosError<ApiErrorBody>
      const msg =
        axiosErr.response?.data?.error?.message ??
        (editTarget !== null ? 'Failed to update department.' : 'Failed to create department.')
      showSnackbar(msg, 'error')
    }
  }

  // ------------------------------------------------------------------
  // Delete
  // ------------------------------------------------------------------

  const handleDeleteClick = (dept: Department) => {
    setDeleteTarget(dept)
    setDeleteDialogOpen(true)
  }

  const handleDeleteConfirm = async () => {
    if (deleteTarget === null) return
    setDeleteLoading(true)
    try {
      await departmentsApi.delete(deleteTarget.id)
      showSnackbar('Department deactivated.', 'success')
      setDeleteDialogOpen(false)
      setDeleteTarget(null)
      await loadDepartments()
    } catch (err) {
      const axiosErr = err as AxiosError<ApiErrorBody>
      if (axiosErr.response?.status === 409) {
        showSnackbar('Cannot delete — employees assigned', 'error')
      } else {
        showSnackbar('Failed to delete department.', 'error')
      }
      setDeleteDialogOpen(false)
    } finally {
      setDeleteLoading(false)
    }
  }

  const handleDeleteCancel = () => {
    if (!deleteLoading) {
      setDeleteDialogOpen(false)
      setDeleteTarget(null)
    }
  }

  // ------------------------------------------------------------------
  // DataGrid columns
  // ------------------------------------------------------------------

  const columns: GridColDef[] = [
    { field: 'name', headerName: 'Name', flex: 1, minWidth: 160 },
    {
      field: 'description',
      headerName: 'Description',
      flex: 2,
      minWidth: 200,
      valueGetter: (params: GridRenderCellParams) =>
        (params.value as string | null) ?? '—',
    },
    {
      field: 'employeeCount',
      headerName: 'Employees',
      width: 110,
      type: 'number',
    },
    {
      field: 'isActive',
      headerName: 'Status',
      width: 120,
      renderCell: (params: GridRenderCellParams) =>
        (params.value as boolean) ? (
          <Chip label="Active" color="success" size="small" />
        ) : (
          <Chip label="Inactive" size="small" sx={{ opacity: 0.6 }} />
        ),
    },
    {
      field: 'actions',
      headerName: 'Actions',
      width: 120,
      sortable: false,
      renderCell: (params: GridRenderCellParams) => {
        const row = params.row as Department
        return (
          <Box>
            <IconButton
              size="small"
              aria-label="edit"
              onClick={() => { handleEditClick(row) }}
            >
              <EditIcon fontSize="small" />
            </IconButton>
            <IconButton
              size="small"
              color="error"
              aria-label="delete"
              onClick={() => { handleDeleteClick(row) }}
            >
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Box>
        )
      },
    },
  ]

  // ------------------------------------------------------------------
  // Render
  // ------------------------------------------------------------------

  if (loading && departments.length === 0) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight={300}>
        <CircularProgress />
      </Box>
    )
  }

  return (
    <Box p={3}>
      {/* Page header */}
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={2}>
        <Typography variant="h5" fontWeight={600}>
          Departments
        </Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={handleAddClick}
        >
          Add Department
        </Button>
      </Box>

      {/* DataGrid */}
      <Box sx={{ height: 520 }}>
        <DataGrid
          rows={departments}
          columns={columns}
          getRowId={(row: Department) => row.id}
          pageSize={20}
          rowsPerPageOptions={[10, 20, 50]}
          disableSelectionOnClick
          loading={loading}
          getRowClassName={(params) =>
            (params.row as Department).isActive ? '' : 'row-inactive'
          }
          sx={{
            '& .row-inactive': {
              opacity: 0.55,
              backgroundColor: 'action.hover',
            },
            '& .MuiDataGrid-cell:focus': { outline: 'none' },
          }}
        />
      </Box>

      {/* Add / Edit dialog */}
      <Dialog open={dialogOpen} onClose={handleDialogClose} maxWidth="sm" fullWidth>
        <form onSubmit={(e) => { void handleSubmit(onSubmit)(e) }}>
          <DialogTitle>
            {editTarget !== null ? 'Edit Department' : 'Add Department'}
          </DialogTitle>
          <DialogContent>
            <TextField
              label="Name"
              fullWidth
              margin="normal"
              {...register('name', { required: 'Department name is required.' })}
              error={errors.name !== undefined}
              helperText={errors.name?.message}
              autoFocus
            />
            <TextField
              label="Description (optional)"
              fullWidth
              margin="normal"
              multiline
              rows={3}
              {...register('description')}
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={handleDialogClose} disabled={isSubmitting}>
              Cancel
            </Button>
            <Button type="submit" variant="contained" disabled={isSubmitting}>
              {editTarget !== null ? 'Save Changes' : 'Create'}
            </Button>
          </DialogActions>
        </form>
      </Dialog>

      {/* Delete confirmation dialog */}
      <Dialog open={deleteDialogOpen} onClose={handleDeleteCancel} maxWidth="xs" fullWidth>
        <DialogTitle>Deactivate Department</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Deactivate <strong>{deleteTarget?.name ?? ''}</strong>? This will hide it from
            active department lists. Employees already assigned will not be moved.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleDeleteCancel} disabled={deleteLoading}>
            Cancel
          </Button>
          <Button
            color="error"
            variant="contained"
            disabled={deleteLoading}
            onClick={() => { void handleDeleteConfirm() }}
          >
            {deleteLoading ? 'Deleting…' : 'Deactivate'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Snackbar for success / error toasts */}
      <Snackbar
        open={snackbar.open}
        autoHideDuration={5000}
        onClose={handleSnackbarClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          onClose={handleSnackbarClose}
          severity={snackbar.severity}
          sx={{ width: '100%' }}
        >
          {snackbar.message}
        </Alert>
      </Snackbar>
    </Box>
  )
}

export default DepartmentsPage
