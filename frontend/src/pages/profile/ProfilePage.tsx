import { useState, useEffect } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Paper,
  Snackbar,
  TextField,
  Typography,
} from '@mui/material';
import { useForm, Controller } from 'react-hook-form';
import { getOwnProfile, updateOwnProfile, type Employee } from '../../api/employeesApi';

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

interface ProfileFormValues {
  firstName: string;
  lastName: string;
  phone: string;
}

/**
 * ProfilePage — all authenticated users can view and edit their own profile.
 *
 * Read-only fields: email, role, department, employeeCode, managerName.
 * Editable fields: firstName, lastName, phone.
 * Calls PUT /api/v1/employees/me.
 *
 * Route: /profile
 * Guard: ProtectedRoute (any authenticated user)
 */
export default function ProfilePage() {
  const [profile, setProfile] = useState<Employee | null>(null);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [snackbar, setSnackbar] = useState<{
    msg: string;
    severity: 'success' | 'error';
  } | null>(null);

  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ProfileFormValues>({
    defaultValues: { firstName: '', lastName: '', phone: '' },
  });

  useEffect(() => {
    void (async () => {
      setLoading(true);
      setFetchError(null);
      try {
        const data = await getOwnProfile();
        setProfile(data);
        reset({
          firstName: data.firstName,
          lastName: data.lastName,
          phone: data.phone ?? '',
        });
      } catch {
        setFetchError('Failed to load profile. Please try again.');
      } finally {
        setLoading(false);
      }
    })();
  }, [reset]);

  const onSubmit = async (values: ProfileFormValues) => {
    setSaving(true);
    try {
      const updated = await updateOwnProfile({
        firstName: values.firstName,
        lastName: values.lastName,
        phone: values.phone || null,
      });
      setProfile(updated);
      setSnackbar({ msg: 'Profile updated successfully.', severity: 'success' });
    } catch {
      setSnackbar({ msg: 'Update failed. Please try again.', severity: 'error' });
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" mt={8}>
        <CircularProgress />
      </Box>
    );
  }

  if (fetchError) {
    return (
      <Box p={4}>
        <Alert severity="error">{fetchError}</Alert>
      </Box>
    );
  }

  return (
    <Box p={4} maxWidth={640}>
      <Typography variant="h5" fontWeight={600} mb={3}>
        My Profile
      </Typography>

      {/* Read-only identity card */}
      {profile && (
        <Paper elevation={2} sx={{ p: 3, mb: 3 }}>
          <Box
            display="grid"
            gridTemplateColumns="1fr 1fr"
            gap={2}
          >
            <Box>
              <Typography variant="caption" color="text.secondary">
                Employee Code
              </Typography>
              <Typography variant="body1" fontWeight={500}>
                {profile.employeeCode}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Email
              </Typography>
              <Typography variant="body1">{profile.email}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Department
              </Typography>
              <Typography variant="body1">
                {profile.departmentName ?? '—'}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Manager
              </Typography>
              <Typography variant="body1">
                {profile.managerName ?? '—'}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary" display="block">
                Role
              </Typography>
              <Box mt={0.5}>
                <Chip
                  label={profile.role}
                  color={roleChipColor(profile.role)}
                  size="small"
                />
              </Box>
            </Box>
          </Box>
        </Paper>
      )}

      {/* Editable fields */}
      <Paper elevation={2} sx={{ p: 3 }}>
        <Typography variant="h6" mb={2}>
          Edit Profile
        </Typography>
        <Box
          component="form"
          onSubmit={handleSubmit(onSubmit)}
          display="flex"
          flexDirection="column"
          gap={2}
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
            name="phone"
            control={control}
            render={({ field }) => (
              <TextField {...field} label="Phone (optional)" fullWidth />
            )}
          />
          <Box display="flex" justifyContent="flex-end">
            <Button type="submit" variant="contained" disabled={saving}>
              {saving ? 'Saving…' : 'Save Changes'}
            </Button>
          </Box>
        </Box>
      </Paper>

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
