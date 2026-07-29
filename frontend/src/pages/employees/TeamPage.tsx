import { useState, useEffect } from 'react';
import {
  Alert,
  Box,
  CircularProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useSelector } from 'react-redux';
import type { RootState } from '../../store';
import { fetchTeam, type Employee } from '../../api/employeesApi';

/**
 * TeamPage — Manager role only.
 *
 * Lists direct reports fetched from GET /api/v1/employees/{id}/team
 * where id = current user's id from Redux auth state.
 *
 * Route: /employees/team
 * Guard: RoleProtectedRoute allowedRoles={['Manager']}
 */
export default function TeamPage() {
  const { user } = useSelector((state: RootState) => state.auth);
  const [team, setTeam] = useState<Employee[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);

  useEffect(() => {
    if (!user) return;
    void (async () => {
      setLoading(true);
      setFetchError(null);
      try {
        const result = await fetchTeam(user.id);
        setTeam(result.items);
      } catch {
        setFetchError('Failed to load team. Please try again.');
      } finally {
        setLoading(false);
      }
    })();
  }, [user]);

  return (
    <Box p={4}>
      <Typography variant="h5" fontWeight={600} mb={3}>
        My Team
      </Typography>

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

      {!loading && !fetchError && team.length === 0 && (
        <Box
          display="flex"
          justifyContent="center"
          mt={6}
          data-testid="empty-state"
        >
          <Typography color="text.secondary">No direct reports.</Typography>
        </Box>
      )}

      {!loading && !fetchError && team.length > 0 && (
        <TableContainer component={Paper} elevation={2}>
          <Table aria-label="team table" data-testid="team-table">
            <TableHead>
              <TableRow>
                <TableCell>
                  <strong>Name</strong>
                </TableCell>
                <TableCell>
                  <strong>Email</strong>
                </TableCell>
                <TableCell>
                  <strong>Department</strong>
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {team.map((member) => (
                <TableRow
                  key={member.id}
                  data-testid={`row-${member.id}`}
                >
                  <TableCell>
                    {member.firstName} {member.lastName}
                  </TableCell>
                  <TableCell>{member.email}</TableCell>
                  <TableCell>{member.departmentName ?? '—'}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
