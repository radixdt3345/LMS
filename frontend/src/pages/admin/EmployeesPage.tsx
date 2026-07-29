import { useState, useEffect, useCallback } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Snackbar,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { useForm, Controller } from 'react-hook-form';
import {
  fetchEmployees,
  createEmployee,
  updateEmployee,
  deactivateEmployee,
  type Employee,
  type CreateEmployeeDto,
  type UpdateEmployeeDto,
} from '../../api/employeesApi';
import { fetchDepartments, type Department } from '../../api/departmentsApi';

type RoleChipColor =
  | 'default'
  | 'primary'
  | 'secondary'
  | 'error'
  | 'info'
  | 'success'
  | 'warning';

function roleChipColor(role: string): RoleChipColor {
  switch (role) {
    case 'SuperAdmin':
      return 'error';
    case 'HRAdmin':
      return 'warning';
    case 'Manager':
      return 'primary';
    default:
      return 'default';
  }
}

interface EmployeeFormValues {
  firstName: string;
  lastName: string;
  email: string;
  employeeCode: string;
  phone: string;
  departmentId: string;
  managerId: string;
}

const emptyForm: EmployeeFormValues = {
  firstName: '',
  lastName: '',
  email: '',
  employeeCode: '',
  phone: '',
  departmentId: '',
  managerId: '',
};

/**
 * EmployeesPage — FR-12, FR-13.
 *
 * HR Admin employee management: list, add, edit, deactivate.
 * Role is derived from manager assignment — never editable directly.
 *
 * Route: /admin/employees
 * Guard: RoleProtectedRoute allowedRoles={['HRAdmin', 'SuperAdmin']}
 */
export default function EmployeesPage() {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [managers, setManagers] = useState<Employee[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);
  const [snackbar, setSnackbar] = useState<{
    msg: string;
    severity: 'success' | 'error';
  } | null>(null);

  // Add / Edit dialog
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingEmployee, setEditingEmployee] = useState<Employee | null>(null);
  const [saving, setSaving] = useState(false);

  // Deactivate confirmation
  const [confirmTarget, setConfirmTarget] = useState<Employee | null>(null);
  const [deactivating, setDeactivating] = useState(false);

  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<EmployeeFormValues>({ defaultValues: emptyForm });

  const loadData = useCallback(async () => {
    setLoading(true);
    setFetchError(null);
    try {
      const [empResult, mgrResult, deptResult] = await Promise.all([
        fetchEmployees(1, 100),
        fetchEmployees(1, 100, 'Manager'),
        fetchDepartments(1, 100),
      ]);
      setEmployees(empResult.items);
      setManagers(mgrResult.items);
      setDepartments(deptResult.items);
    } catch {
      setFetchError('Failed to load employees. Please try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const openAddDialog = () => {
    setEditingEmployee(null);
    reset(emptyForm);
    setDialogOpen(true);
  };

  const openEditDialog = (emp: Employee) => {
    setEditingEmployee(emp);
    reset({
      firstName: emp.firstName,
      lastName: emp.lastName,
      email: emp.email,
      employeeCode: emp.employeeCode,
      phone: emp.phone ?? '',
      departmentId: emp.departmentId ?? '',
      managerId: emp.managerId ?? '',
    });
    setDialogOpen(true);
  };

  const handleDialogClose = () => {
    setDialogOpen(false);
    setEditingEmployee(null);
  };

  const onSubmit = async (values: EmployeeFormValues) => {
    setSaving(true);
    try {
      if (editingEmployee) {
        const dto: UpdateEmployeeDto = {
          firstName: values.firstName,
          lastName: values.lastName,
          email: values.email,
          employeeCode: values.employeeCode,
          phone: values.phone || null,
          departmentId: values.departmentId || null,
          managerId: values.managerId || null,
        };
        const updated = await updateEmployee(editingEmployee.id, dto);
        setEmployees((prev) =>
          prev.map((e) => (e.id === updated.id ? updated : e)),
        );
        setSnackbar({ msg: 'Employee updated.', severity: 'success' });
      } else {
        const dto: CreateEmployeeDto = {
          firstName: values.firstName,
          lastName: values.lastName,
          email: values.email,
          employeeCode: values.employeeCode,
          phone: values.phone || null,
          departmentId: values.departmentId || null,
          managerId: values.managerId || null,
        };
        const created = await createEmployee(dto);
        setEmployees((prev) => [...prev, created]);
        setSnackbar({ msg: 'Employee created.', severity: 'success' });
      }
      handleDialogClose();
    } catch {
      setSnackbar({ msg: 'Save failed. Please try again.', severity: 'error' });
    } finally {
      setSaving(false);
    }
  };

  const handleDeactivate = async () => {
    if (!confirmTarget) return;
    setDeactivating(true);
    try {
      await deactivateEmployee(confirmTarget.id);
      setEmployees((prev) =>
        prev.map((e) =>
          e.id === confirmTarget.id ? { ...e, isActive: false } : e,
        ),
      );
      setSnackbar({
        msg: `${confirmTarget.firstName} ${confirmTarget.lastName} deactivated.`,
        severity: 'success',
      });
    } catch {
      setSnackbar({
        msg: 'Deactivation failed. Please try again.',
        severity: 'error',
      });
    } finally {
      setDeactivating(false);
      setConfirmTarget(null);
    }
  };

  return (
    <Box p={4}>
      <Box
        display="flex"
        justifyContent="space-between"
        alignItems="center"
        mb={3}
      >
        <Typography variant="h5" fontWeight={600}>
          Employees
        </Typography>
        <Button variant="contained" onClick={openAddDialog}>
          Add Employee
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

      {!loading && !fetchError && (
        <TableContainer component={Paper} elevation={2}>
          <Table aria-label="employees table" data-testid="employees-table">
            <TableHead>
              <TableRow>
                <TableCell>
                  <strong>Code</strong>
                </TableCell>
                <TableCell>
                  <strong>Name</strong>
                </TableCell>
                <TableCell>
                  <strong>Department</strong>
                </TableCell>
                <TableCell>
                  <strong>Manager</strong>
                </TableCell>
                <TableCell>
                  <strong>Role</strong>
                </TableCell>
                <TableCell>
                  <strong>Status</strong>
                </TableCell>
                <TableCell align="center">
                  <strong>Actions</strong>
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {employees.map((emp) => (
                <TableRow
                  key={emp.id}
                  data-testid={`row-${emp.id}`}
                  sx={{ opacity: emp.isActive ? 1 : 0.5 }}
                >
                  <TableCell>{emp.employeeCode}</TableCell>
                  <TableCell>
                    {emp.firstName} {emp.lastName}
                  </TableCell>
                  <TableCell>{emp.departmentName ?? '—'}</TableCell>
                  <TableCell>{emp.managerName ?? '—'}</TableCell>
                  <TableCell>
                    <Chip
                      label={emp.role}
                      color={roleChipColor(emp.role)}
                      size="small"
                    />
                  </TableCell>
                  <TableCell>
                    {emp.isActive ? (
                      <Chip label="Active" color="success" size="small" />
                    ) : (
                      <Chip label="Inactive" color="default" size="small" />
                    )}
                  </TableCell>
                  <TableCell align="center">
                    <Button
                      size="small"
                      variant="outlined"
                      onClick={() => openEditDialog(emp)}
                      sx={{ mr: 1 }}
                      data-testid={`edit-btn-${emp.id}`}
                    >
                      Edit
                    </Button>
                    {emp.isActive && (
                      <Button
                        size="small"
                        variant="outlined"
                        color="error"
                        onClick={() => setConfirmTarget(emp)}
                        data-testid={`deactivate-btn-${emp.id}`}
                      >
                        Deactivate
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {employees.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} align="center">
                    <Typography color="text.secondary" py={3}>
                      No employees found.
                    </Typography>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Add / Edit Dialog */}
      <Dialog
        open={dialogOpen}
        onClose={handleDialogClose}
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle>
          {editingEmployee ? 'Edit Employee' : 'Add Employee'}
        </DialogTitle>
        <DialogContent>
          <Box
            component="form"
            id="employee-form"
            onSubmit={handleSubmit(onSubmit)}
            display="flex"
            flexDirection="column"
            gap={2}
            mt={1}
          >
            <Controller
              name="firstName"
              control={control}
              rules={{ required: 'First name is required.' }}
              render={({ field }) => (
                <TextField
                  {...field}
                  label="First Name"
                  fullWidth
                  error={!!errors.firstName}
                  helperText={errors.firstName?.message}
                />
              )}
            />
            <Controller
              name="lastName"
              control={control}
              rules={{ required: 'Last name is required.' }}
              render={({ field }) => (
                <TextField
                  {...field}
                  label="Last Name"
                  fullWidth
                  error={!!errors.lastName}
                  helperText={errors.lastName?.message}
                />
              )}
            />
            <Controller
              name="email"
              control={control}
              rules={{
                required: 'Email is required.',
                pattern: {
                  value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
                  message: 'Invalid email address.',
                },
              }}
              render={({ field }) => (
                <TextField
                  {...field}
                  label="Email"
                  type="email"
                  fullWidth
                  error={!!errors.email}
                  helperText={errors.email?.message}
                />
              )}
            />
            <Controller
              name="employeeCode"
              control={control}
              rules={{ required: 'Employee code is required.' }}
              render={({ field }) => (
                <TextField
                  {...field}
                  label="Employee Code"
                  fullWidth
                  error={!!errors.employeeCode}
                  helperText={errors.employeeCode?.message}
                />
              )}
            />
            <Controller
              name="phone"
              control={control}
              render={({ field }) => (
                <TextField {...field} label="Phone (optional)" fullWidth />
              )}
            />
            <Controller
              name="departmentId"
              control={control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="dept-label">Department</InputLabel>
                  <Select
                    {...field}
                    labelId="dept-label"
                    label="Department"
                  >
                    <MenuItem value="">
                      <em>None</em>
                    </MenuItem>
                    {departments.map((d) => (
                      <MenuItem key={d.id} value={d.id}>
                        {d.name}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller
              name="managerId"
              control={control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="mgr-label">Manager</InputLabel>
                  <Select
                    {...field}
                    labelId="mgr-label"
                    label="Manager"
                  >
                    <MenuItem value="">
                      <em>None</em>
                    </MenuItem>
                    {managers.map((m) => (
                      <MenuItem key={m.id} value={m.id}>
                        {m.firstName} {m.lastName}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            {/* Role is derived from manager assignment — read-only badge */}
            {editingEmployee && (
              <Box>
                <Typography variant="caption" color="text.secondary">
                  Role (derived from manager assignment — not editable)
                </Typography>
                <Box mt={0.5}>
                  <Chip
                    label={editingEmployee.role}
                    color={roleChipColor(editingEmployee.role)}
                    size="small"
                  />
                </Box>
              </Box>
            )}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleDialogClose} disabled={saving}>
            Cancel
          </Button>
          <Button
            type="submit"
            form="employee-form"
            variant="contained"
            disabled={saving}
          >
            {saving ? 'Saving…' : 'Save'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Deactivate confirmation dialog */}
      <Dialog
        open={confirmTarget !== null}
        onClose={() => setConfirmTarget(null)}
      >
        <DialogTitle>Deactivate Employee</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Are you sure you want to deactivate{' '}
            <strong>
              {confirmTarget?.firstName} {confirmTarget?.lastName}
            </strong>
            ? Their account will be disabled.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button
            onClick={() => setConfirmTarget(null)}
            disabled={deactivating}
          >
            Cancel
          </Button>
          <Button
            color="error"
            variant="contained"
            onClick={() => void handleDeactivate()}
            disabled={deactivating}
            data-testid="confirm-deactivate-btn"
          >
            {deactivating ? 'Deactivating…' : 'Deactivate'}
          </Button>
        </DialogActions>
      </Dialog>

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
          {snackbar?.msg}
        </Alert>
      </Snackbar>
    </Box>
  );
}
