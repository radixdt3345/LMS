import React, { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import {
  submitCompOffRequest,
  type CreateCompOffRequestDto,
} from '../../api/compOffApi';

/** Credit-days preview: hoursWorked / 8, rounded to nearest 0.5. */
function computeCreditDays(hours: number): number {
  return Math.round((hours / 8) * 2) / 2;
}

/** ISO YYYY-MM-DD string for yesterday (inclusive upper bound for date picker). */
function yesterday(): string {
  const d = new Date();
  d.setDate(d.getDate() - 1);
  return d.toISOString().slice(0, 10);
}

export default function NewCompOffPage(): React.JSX.Element {
  const [workedDate, setWorkedDate]   = useState('');
  const [hoursWorked, setHoursWorked] = useState('8');
  const [reason, setReason]           = useState('');
  const [submitting, setSubmitting]   = useState(false);
  const [error, setError]             = useState<string | null>(null);
  const [success, setSuccess]         = useState(false);

  const hours      = parseFloat(hoursWorked) || 0;
  const creditDays = computeCreditDays(hours);
  const hoursValid = hours >= 4 && hours <= 16 && hours % 0.5 === 0;
  const dateValid  = workedDate.length > 0 && workedDate <= yesterday();
  const reasonValid = reason.trim().length >= 10;
  const canSubmit   = hoursValid && dateValid && reasonValid && !submitting;

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>): Promise<void> {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const dto: CreateCompOffRequestDto = {
        workedDate,
        hoursWorked: hours,
        reason:      reason.trim(),
      };
      await submitCompOffRequest(dto);
      setSuccess(true);
      setWorkedDate('');
      setHoursWorked('8');
      setReason('');
    } catch (err: unknown) {
      setError(
        err instanceof Error
          ? err.message
          : 'Failed to submit request. Please try again.',
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Box maxWidth={580} mx="auto" mt={4} px={2}>
      <Typography variant="h5" fontWeight={600} gutterBottom>
        New Comp-Off Request
      </Typography>
      <Typography variant="body2" color="text.secondary" mb={3}>
        Submit a comp-off request for hours worked on a non-working day. Your manager
        will review and approve or reject it.
      </Typography>

      {success && (
        <Alert
          severity="success"
          sx={{ mb: 2 }}
          onClose={() => setSuccess(false)}
        >
          Request submitted successfully. Awaiting manager approval.
        </Alert>
      )}
      {error !== null && (
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          onClose={() => setError(null)}
        >
          {error}
        </Alert>
      )}

      <Card variant="outlined">
        <CardContent>
          <form onSubmit={(e) => { void handleSubmit(e); }} noValidate>
            <Stack spacing={3}>
              {/* Worked date — past dates only (max = yesterday) */}
              <TextField
                label="Date Worked"
                type="date"
                required
                fullWidth
                value={workedDate}
                onChange={(e) => setWorkedDate(e.target.value)}
                inputProps={{ max: yesterday() }}
                InputLabelProps={{ shrink: true }}
                helperText="Select the date you worked on a weekend or public holiday."
                error={workedDate.length > 0 && !dateValid}
              />

              {/* Hours worked — min 4, step 0.5 */}
              <TextField
                label="Hours Worked"
                type="number"
                required
                fullWidth
                value={hoursWorked}
                onChange={(e) => setHoursWorked(e.target.value)}
                inputProps={{ min: 4, max: 16, step: 0.5 }}
                helperText="Minimum 4 h (half-day). 4 h = 0.5 credit day, 8 h = 1.0 credit day."
                error={hoursWorked.length > 0 && !hoursValid}
              />

              {/* Credit-days preview */}
              {hoursValid && (
                <Box display="flex" alignItems="center" gap={1.5}>
                  <Typography variant="body2" color="text.secondary">
                    Credit days you will earn:
                  </Typography>
                  <Chip
                    label={`${creditDays} day${creditDays === 1 ? '' : 's'}`}
                    color="primary"
                    size="small"
                  />
                </Box>
              )}

              {/* Reason */}
              <TextField
                label="Reason"
                multiline
                rows={3}
                required
                fullWidth
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                helperText={`Brief description of the work done (min 10 chars). ${reason.trim().length}/500`}
                error={reason.length > 0 && !reasonValid}
                inputProps={{ maxLength: 500 }}
              />

              <Button
                type="submit"
                variant="contained"
                disabled={!canSubmit}
                startIcon={
                  submitting
                    ? <CircularProgress size={16} color="inherit" />
                    : null
                }
              >
                {submitting ? 'Submitting…' : 'Submit Request'}
              </Button>
            </Stack>
          </form>
        </CardContent>
      </Card>
    </Box>
  );
}
