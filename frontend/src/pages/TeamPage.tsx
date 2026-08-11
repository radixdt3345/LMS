/**
 * TeamPage — Manager only
 *
 * Shows the manager's direct reports (team members).
 *
 * Route: /team
 * Guard: RoleProtectedRoute allowedRoles=['Manager']
 */
import { useEffect, useState } from 'react';
import { useSelector } from 'react-redux';
import {
  Alert,
  Avatar,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Grid,
  Typography,
} from '@mui/material';
import { fetchTeam, type Employee } from '../api/employeesApi';
import type { RootState } from '../store';

function initials(emp: Employee): string {
  return `${emp.firstName[0] ?? ''}${emp.lastName[0] ?? ''}`.toUpperCase();
}

export default function TeamPage() {
  const userId = useSelector((state: RootState) => state.auth.user?.id);
  const [team, setTeam] = useState<Employee[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!userId) return;
    setLoading(true);
    fetchTeam(userId)
      .then(result => setTeam(result.items))
      .catch(() => setError('Failed to load team members.'))
      .finally(() => setLoading(false));
  }, [userId]);

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight={300}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box p={4}>
      <Typography variant="h4" mb={3}>
        My Team
      </Typography>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {team.length === 0 ? (
        <Typography color="text.secondary">No direct reports found.</Typography>
      ) : (
        <Grid container spacing={2}>
          {team.map(emp => (
            <Grid item xs={12} sm={6} md={4} key={emp.id}>
              <Card variant="outlined">
                <CardContent>
                  <Box display="flex" alignItems="center" gap={2} mb={1}>
                    <Avatar sx={{ bgcolor: 'primary.main' }}>
                      {initials(emp)}
                    </Avatar>
                    <Box flex={1} minWidth={0}>
                      <Typography variant="subtitle1" fontWeight={600} noWrap>
                        {emp.firstName} {emp.lastName}
                      </Typography>
                      <Typography variant="body2" color="text.secondary" noWrap>
                        {emp.email}
                      </Typography>
                    </Box>
                  </Box>
                  <Box display="flex" gap={1} flexWrap="wrap" mt={1}>
                    <Chip label={emp.role} size="small" color="primary" variant="outlined" />
                    {emp.departmentName && (
                      <Chip label={emp.departmentName} size="small" variant="outlined" />
                    )}
                    {!emp.isActive && (
                      <Chip label="Inactive" size="small" color="error" />
                    )}
                  </Box>
                  {emp.phone && (
                    <Typography variant="caption" color="text.secondary" display="block" mt={1}>
                      {emp.phone}
                    </Typography>
                  )}
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}

      <Typography variant="caption" color="text.secondary" display="block" mt={3}>
        {team.length} team member{team.length !== 1 ? 's' : ''}
      </Typography>
    </Box>
  );
}
