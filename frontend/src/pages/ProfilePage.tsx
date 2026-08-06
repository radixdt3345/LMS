/**
 * ProfilePage — all authenticated users
 *
 * Shows own profile (read-only fields: email, employeeCode, role, department, manager)
 * and allows editing: firstName, lastName, phone.
 *
 * Route: /profile
 * Guard: ProtectedRoute (any authenticated user)
 */
import { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Divider,
  Grid,
  TextField,
  Typography,
} from '@mui/material';
import {
  getOwnProfile,
  updateOwnProfile,
  type Employee,
} from '../api/employeesApi';

export default function ProfilePage() {
  const [profile, setProfile] = useState<Employee | null>(null);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);

  // Edit form
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [phone, setPhone] = useState('');
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);

  useEffect(() => {
    setLoading(true);
    getOwnProfile()
      .then(p => {
        setProfile(p);
        setFirstName(p.firstName);
        setLastName(p.lastName);
        setPhone(p.phone ?? '');
      })
      .catch(() => setFetchError('Failed to load profile.'))
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async () => {
    if (!firstName.trim() || !lastName.trim()) return;
    setSaving(true);
    setSaveError(null);
    setSaveSuccess(false);
    try {
      const updated = await updateOwnProfile({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        phone: phone.trim() || null,
      });
      setProfile(updated);
      setSaveSuccess(true);
    } catch {
      setSaveError('Failed to save profile. Please try again.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight={300}>
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
    <Box p={4} maxWidth={700} mx="auto">
      <Typography variant="h4" mb={3}>
        My Profile
      </Typography>

      <Card variant="outlined">
        <CardContent>
          {/* Read-only info */}
          <Typography variant="subtitle1" fontWeight={600} mb={2}>
            Account Information
          </Typography>
          <Grid container spacing={2} mb={2}>
            <Grid item xs={12} sm={6}>
              <TextField
                label="Email"
                value={profile?.email ?? ''}
                fullWidth
                InputProps={{ readOnly: true }}
                variant="filled"
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                label="Employee Code"
                value={profile?.employeeCode ?? ''}
                fullWidth
                InputProps={{ readOnly: true }}
                variant="filled"
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                label="Role"
                value={profile?.role ?? ''}
                fullWidth
                InputProps={{ readOnly: true }}
                variant="filled"
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                label="Department"
                value={profile?.departmentName ?? 'None'}
                fullWidth
                InputProps={{ readOnly: true }}
                variant="filled"
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                label="Manager"
                value={profile?.managerName ?? 'None'}
                fullWidth
                InputProps={{ readOnly: true }}
                variant="filled"
              />
            </Grid>
          </Grid>

          <Divider sx={{ my: 3 }} />

          {/* Editable fields */}
          <Typography variant="subtitle1" fontWeight={600} mb={2}>
            Edit Profile
          </Typography>

          {saveSuccess && (
            <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSaveSuccess(false)}>
              Profile updated successfully.
            </Alert>
          )}
          {saveError && (
            <Alert severity="error" sx={{ mb: 2 }} onClose={() => setSaveError(null)}>
              {saveError}
            </Alert>
          )}

          <Grid container spacing={2}>
            <Grid item xs={12} sm={6}>
              <TextField
                label="First Name"
                value={firstName}
                onChange={e => setFirstName(e.target.value)}
                fullWidth
                required
                error={!firstName.trim()}
                helperText={!firstName.trim() ? 'Required' : ''}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                label="Last Name"
                value={lastName}
                onChange={e => setLastName(e.target.value)}
                fullWidth
                required
                error={!lastName.trim()}
                helperText={!lastName.trim() ? 'Required' : ''}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                label="Phone"
                value={phone}
                onChange={e => setPhone(e.target.value)}
                fullWidth
                placeholder="+91 9876543210"
              />
            </Grid>
          </Grid>

          <Box display="flex" justifyContent="flex-end" mt={3}>
            <Button
              variant="contained"
              onClick={() => void handleSave()}
              disabled={saving || !firstName.trim() || !lastName.trim()}
            >
              {saving ? <CircularProgress size={20} color="inherit" /> : 'Save Changes'}
            </Button>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}
