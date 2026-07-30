import { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  type SelectChangeEvent,
  Snackbar,
  TextField,
  Typography,
} from '@mui/material';
import {
  getLeaveTypes,
  createLeaveRequest,
  submitLeaveRequest,
  previewLeaveDays,
  type LeaveTypeDto,
} from '../../api/leaveRequestsApi';

/**
 * NewLeavePage — FR-18, FR-19.
 *
 * Allows an authenticated employee to submit a new leave request.
 *   1. Fill in dates, leave type, reason, optional document URL.
 *   2. Live preview of computed working days (debounced 500 ms).
 *   3. Submit: POST /create → POST /submit → navigate to /leaves/history.
 *
 * Route: /leaves/new
 * Guard: ProtectedRoute (any authenticated user)
 */
export default function NewLeavePage() {
  const navigate = useNavigate();

  // -------------------------------------------------------------------------
  // Form state
  // -------------------------------------------------------------------------
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [leaveTypeId, setLeaveTypeId] = useState('');
  const [reason, setReason] = useState('');
  const [documentUrl, setDocumentUrl] = useState('');

  // -------------------------------------------------------------------------
  // Derived / remote state
  // -------------------------------------------------------------------------
  const [leaveTypes, setLeaveTypes] = useState<LeaveTypeDto[]>([]);
  const [leaveTypesLoading, setLeaveTypesLoading] = useState(true);
  const [leaveTypesError, setLeaveTypesError] = useState<string | null>(null);

  const [computedDays, setComputedDays] = useState<number | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);

  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Validation errors
  const [errors, setErrors] = useState<Record<string, string>>({});

  // Snackbar for non-critical messages
  const [snackbar, setSnackbar] = useState<string | null>(null);

  // -------------------------------------------------------------------------
  // Derived helpers
  // -------------------------------------------------------------------------
  const selectedLeaveType = leaveTypes.find((lt) => lt.id === leaveTypeId);
  const requiresDocument = selectedLeaveType?.requiresDocument ?? false;

  // -------------------------------------------------------------------------
  // Load leave types on mount
  // -------------------------------------------------------------------------
  useEffect(() => {
    let cancelled = false;
    setLeaveTypesLoading(true);
    setLeaveTypesError(null);

    getLeaveTypes()
      .then((types) => {
        if (!cancelled) {
          setLeaveTypes(types);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setLeaveTypesError('Failed to load leave types. Please refresh.');
        }
      })
      .finally(() => {
        if (!cancelled) setLeaveTypesLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  // -------------------------------------------------------------------------
  // Live preview — debounced 500 ms
  // -------------------------------------------------------------------------
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const fetchPreview = useCallback(
    (start: string, end: string, typeId: string) => {
      if (!start || !end || !typeId || start > end) {
        setComputedDays(null);
        return;
      }
      setPreviewLoading(true);
      previewLeaveDays(start, end, typeId)
        .then((res) => {
          setComputedDays(res.computed_days);
        })
        .catch(() => {
          setComputedDays(null);
        })
        .finally(() => {
          setPreviewLoading(false);
        });
    },
    [],
  );

  useEffect(() => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
    }
    debounceRef.current = setTimeout(() => {
      fetchPreview(startDate, endDate, leaveTypeId);
    }, 500);

    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current);
      }
    };
  }, [startDate, endDate, leaveTypeId, fetchPreview]);

  // -------------------------------------------------------------------------
  // Validation
  // -------------------------------------------------------------------------
  function validate(): boolean {
    const next: Record<string, string> = {};

    if (!startDate) next.startDate = 'Start date is required.';
    if (!endDate) next.endDate = 'End date is required.';
    if (startDate && endDate && startDate > endDate) {
      next.endDate = 'End date must be on or after start date.';
    }
    if (!leaveTypeId) next.leaveTypeId = 'Please select a leave type.';
    if (!reason.trim()) next.reason = 'Reason is required.';
    if (requiresDocument && !documentUrl.trim()) {
      next.documentUrl = 'Document URL is required for this leave type.';
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  // -------------------------------------------------------------------------
  // Submit
  // -------------------------------------------------------------------------
  const handleSubmit = async () => {
    if (!validate()) return;

    setSubmitting(true);
    setSubmitError(null);

    try {
      // Step 1: create draft
      const draft = await createLeaveRequest({
        leaveTypeId,
        startDate,
        endDate,
        reason: reason.trim(),
        documentUrl: requiresDocument && documentUrl.trim() ? documentUrl.trim() : null,
      });

      // Step 2: submit (Draft → Pending)
      await submitLeaveRequest(draft.id);

      navigate('/leaves/history');
    } catch {
      setSubmitError('Failed to submit leave request. Please try again.');
    } finally {
      setSubmitting(false);
    }
  };

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------
  return (
    <Box p={4} maxWidth={600} mx="auto">
      <Typography variant="h5" fontWeight={600} mb={3}>
        New Leave Request
      </Typography>

      {leaveTypesError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {leaveTypesError}
        </Alert>
      )}

      {submitError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {submitError}
        </Alert>
      )}

      <Paper elevation={2} sx={{ p: 3 }}>
        {/* Date range */}
        <Box display="flex" gap={2} mb={2}>
          <TextField
            label="Start Date"
            type="date"
            fullWidth
            InputLabelProps={{ shrink: true }}
            value={startDate}
            onChange={(e) => {
              setStartDate(e.target.value);
              setErrors((prev) => ({ ...prev, startDate: '' }));
            }}
            error={!!errors.startDate}
            helperText={errors.startDate}
            inputProps={{ 'data-testid': 'start-date-input' }}
          />
          <TextField
            label="End Date"
            type="date"
            fullWidth
            InputLabelProps={{ shrink: true }}
            value={endDate}
            onChange={(e) => {
              setEndDate(e.target.value);
              setErrors((prev) => ({ ...prev, endDate: '' }));
            }}
            error={!!errors.endDate}
            helperText={errors.endDate}
            inputProps={{ 'data-testid': 'end-date-input' }}
          />
        </Box>

        {/* Computed days chip */}
        <Box mb={2} minHeight={32} display="flex" alignItems="center">
          {previewLoading && (
            <CircularProgress size={16} sx={{ mr: 1 }} />
          )}
          {!previewLoading && computedDays !== null && (
            <Chip
              label={`${computedDays} working day${computedDays !== 1 ? 's' : ''}`}
              color="primary"
              variant="outlined"
              size="small"
              data-testid="computed-days-chip"
            />
          )}
        </Box>

        {/* Leave type */}
        <FormControl
          fullWidth
          sx={{ mb: 2 }}
          error={!!errors.leaveTypeId}
          disabled={leaveTypesLoading}
        >
          <InputLabel id="leave-type-label">Leave Type</InputLabel>
          <Select
            labelId="leave-type-label"
            label="Leave Type"
            value={leaveTypeId}
            onChange={(e: SelectChangeEvent) => {
              setLeaveTypeId(e.target.value);
              setErrors((prev) => ({ ...prev, leaveTypeId: '' }));
            }}
            inputProps={{ 'data-testid': 'leave-type-select' }}
          >
            {leaveTypes.map((lt) => (
              <MenuItem key={lt.id} value={lt.id}>
                {lt.name}
              </MenuItem>
            ))}
          </Select>
          {errors.leaveTypeId && (
            <FormHelperText>{errors.leaveTypeId}</FormHelperText>
          )}
        </FormControl>

        {/* Reason */}
        <TextField
          label="Reason"
          multiline
          rows={3}
          fullWidth
          sx={{ mb: 2 }}
          value={reason}
          onChange={(e) => {
            setReason(e.target.value);
            setErrors((prev) => ({ ...prev, reason: '' }));
          }}
          error={!!errors.reason}
          helperText={errors.reason}
          inputProps={{ 'data-testid': 'reason-textarea' }}
        />

        {/* Document URL — shown only when requiresDocument */}
        {requiresDocument && (
          <TextField
            label="Document URL"
            type="url"
            fullWidth
            sx={{ mb: 2 }}
            value={documentUrl}
            onChange={(e) => {
              setDocumentUrl(e.target.value);
              setErrors((prev) => ({ ...prev, documentUrl: '' }));
            }}
            error={!!errors.documentUrl}
            helperText={errors.documentUrl ?? 'This leave type requires supporting documentation.'}
            inputProps={{ 'data-testid': 'document-url-input' }}
          />
        )}

        {/* Actions */}
        <Box display="flex" justifyContent="flex-end" gap={2} mt={1}>
          <Button
            variant="outlined"
            onClick={() => navigate('/leaves/history')}
            disabled={submitting}
          >
            Cancel
          </Button>
          <Button
            variant="contained"
            onClick={() => void handleSubmit()}
            disabled={submitting || leaveTypesLoading}
            data-testid="submit-button"
          >
            {submitting ? (
              <CircularProgress size={20} color="inherit" />
            ) : (
              'Submit Request'
            )}
          </Button>
        </Box>
      </Paper>

      <Snackbar
        open={snackbar !== null}
        autoHideDuration={4000}
        onClose={() => setSnackbar(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert onClose={() => setSnackbar(null)} severity="success" sx={{ width: '100%' }}>
          {snackbar}
        </Alert>
      </Snackbar>
    </Box>
  );
}
