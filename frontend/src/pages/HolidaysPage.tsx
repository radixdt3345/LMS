import { useEffect, useRef, useState, useCallback } from 'react';
import { useSelector } from 'react-redux';
import FullCalendar from '@fullcalendar/react';
import dayGridPlugin from '@fullcalendar/daygrid';
import type { EventInput } from '@fullcalendar/core';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  IconButton,
  MenuItem,
  Select,
  Snackbar,
  Switch,
  TextField,
  Tooltip,
  Typography,
  List,
  ListItem,
  ListItemText,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import DeleteIcon from '@mui/icons-material/Delete';
import { useForm, Controller } from 'react-hook-form';
import type { RootState } from '../store';
import {
  fetchHolidays,
  createHoliday,
  deleteHoliday,
  bulkImportHolidays,
  type HolidayDto,
  type BulkImportResult,
} from '../api/holidaysApi';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface AddFormValues {
  date: string;
  name: string;
  isOptional: boolean;
}

// FullCalendar event extended with our holiday ID so we can delete it
interface HolidayEvent extends EventInput {
  extendedProps: { holidayId: string; isOptional: boolean };
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function buildYearOptions(): number[] {
  const current = new Date().getFullYear();
  return [current - 1, current, current + 1, current + 2];
}

const YEAR_OPTIONS = buildYearOptions();

function toCalendarEvents(holidays: HolidayDto[]): HolidayEvent[] {
  return holidays.map((h) => ({
    id: h.id,
    title: h.isOptional ? `${h.name} (Optional)` : h.name,
    start: h.date,
    allDay: true,
    backgroundColor: h.isOptional ? '#f59e0b' : '#ef4444',
    borderColor: h.isOptional ? '#f59e0b' : '#ef4444',
    editable: false,
    extendedProps: { holidayId: h.id, isOptional: h.isOptional },
  }));
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export default function HolidaysPage() {
  const auth = useSelector((state: RootState) => state.auth);
  const isHrAdmin = auth.user?.role === 'HRAdmin';
  const token = auth.accessToken ?? '';

  const [year, setYear] = useState<number>(new Date().getFullYear());
  const [holidays, setHolidays] = useState<HolidayDto[]>([]);
  const [loading, setLoading] = useState(true);

  // Add holiday dialog
  const [addOpen, setAddOpen] = useState(false);
  // Import summary dialog
  const [importResult, setImportResult] = useState<BulkImportResult | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  // File input ref for CSV import
  const csvInputRef = useRef<HTMLInputElement>(null);

  const [toast, setToast] = useState<{
    open: boolean;
    message: string;
    severity: 'success' | 'error';
  }>({ open: false, message: '', severity: 'success' });

  const { control, handleSubmit, reset } = useForm<AddFormValues>({
    defaultValues: { date: '', name: '', isOptional: false },
  });

  // ---------------------------------------------------------------------------
  // Data fetching
  // ---------------------------------------------------------------------------

  const loadHolidays = useCallback(async () => {
    setLoading(true);
    try {
      const data = await fetchHolidays(token, year);
      setHolidays(data);
    } catch {
      showToast('Failed to load holidays', 'error');
    } finally {
      setLoading(false);
    }
  }, [token, year]);

  useEffect(() => {
    void loadHolidays();
  }, [loadHolidays]);

  // When year changes, tell FullCalendar to navigate to that year
  const calendarRef = useRef<FullCalendar>(null);
  useEffect(() => {
    if (calendarRef.current) {
      const api = calendarRef.current.getApi();
      api.gotoDate(`${year}-01-01`);
    }
  }, [year]);

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------

  function showToast(message: string, severity: 'success' | 'error') {
    setToast({ open: true, message, severity });
  }

  // ---------------------------------------------------------------------------
  // Add holiday
  // ---------------------------------------------------------------------------

  async function onAddSubmit(values: AddFormValues) {
    try {
      await createHoliday(token, {
        date: values.date,
        name: values.name.trim(),
        isOptional: values.isOptional,
      });
      showToast('Holiday added', 'success');
      setAddOpen(false);
      reset();
      void loadHolidays();
    } catch {
      showToast('Failed to add holiday', 'error');
    }
  }

  // ---------------------------------------------------------------------------
  // Delete holiday
  // ---------------------------------------------------------------------------

  async function handleDelete(id: string) {
    try {
      await deleteHoliday(token, id);
      showToast('Holiday deleted', 'success');
      void loadHolidays();
    } catch {
      showToast('Failed to delete holiday', 'error');
    }
  }

  // ---------------------------------------------------------------------------
  // CSV import
  // ---------------------------------------------------------------------------

  async function handleCsvChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    // Reset input so the same file can be re-selected
    e.target.value = '';
    try {
      const result = await bulkImportHolidays(token, file);
      setImportResult(result);
      setImportOpen(true);
      void loadHolidays();
    } catch {
      showToast('CSV import failed', 'error');
    }
  }

  // ---------------------------------------------------------------------------
  // Calendar events
  // ---------------------------------------------------------------------------

  const calendarEvents = toCalendarEvents(holidays);

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------

  return (
    <Box p={3}>
      {/* Header */}
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={2} flexWrap="wrap" gap={1}>
        <Typography variant="h5">Holiday Calendar</Typography>

        <Box display="flex" alignItems="center" gap={1}>
          {/* Year selector */}
          <Select
            value={year}
            size="small"
            onChange={(e) => setYear(e.target.value as number)}
            sx={{ minWidth: 100 }}
          >
            {YEAR_OPTIONS.map((y) => (
              <MenuItem key={y} value={y}>{y}</MenuItem>
            ))}
          </Select>

          {/* HR Admin controls */}
          {isHrAdmin && (
            <>
              <Button
                variant="contained"
                startIcon={<AddIcon />}
                onClick={() => { reset(); setAddOpen(true); }}
              >
                Add Holiday
              </Button>

              <Button
                variant="outlined"
                startIcon={<UploadFileIcon />}
                onClick={() => csvInputRef.current?.click()}
              >
                CSV Import
              </Button>
              {/* Hidden file input */}
              <input
                ref={csvInputRef}
                type="file"
                accept=".csv"
                hidden
                onChange={(e) => void handleCsvChange(e)}
              />
            </>
          )}
        </Box>
      </Box>

      {/* Calendar */}
      {loading ? (
        <Box display="flex" justifyContent="center" mt={6}>
          <CircularProgress />
        </Box>
      ) : (
        <Box
          sx={{
            '& .fc-event': { cursor: 'default' },
            '& .fc-daygrid-event-dot': { display: 'none' },
          }}
        >
          <FullCalendar
            ref={calendarRef}
            plugins={[dayGridPlugin]}
            initialView="dayGridMonth"
            initialDate={`${year}-01-01`}
            headerToolbar={{
              left: 'prev,next today',
              center: 'title',
              right: '',
            }}
            events={calendarEvents}
            eventClick={(info) => {
              // Non-clickable for employees/managers; HR Admin gets delete via list below
              info.jsEvent.preventDefault();
            }}
            height="auto"
          />
        </Box>
      )}

      {/* Holiday list with delete (HR Admin only) */}
      {isHrAdmin && holidays.length > 0 && (
        <Box mt={3}>
          <Typography variant="subtitle1" gutterBottom>
            {year} Holidays
          </Typography>
          <List dense disablePadding>
            {holidays.map((h) => (
              <ListItem
                key={h.id}
                secondaryAction={
                  <Tooltip title="Delete">
                    <IconButton
                      edge="end"
                      color="error"
                      size="small"
                      onClick={() => void handleDelete(h.id)}
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                }
              >
                <ListItemText
                  primary={h.name}
                  secondary={`${h.date}${h.isOptional ? ' — Optional' : ''}`}
                />
              </ListItem>
            ))}
          </List>
        </Box>
      )}

      {/* Add Holiday Dialog */}
      <Dialog open={addOpen} onClose={() => setAddOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Add Holiday</DialogTitle>
        {/* eslint-disable-next-line @typescript-eslint/no-misused-promises */}
        <form onSubmit={handleSubmit(onAddSubmit)}>
          <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
            <Controller
              name="date"
              control={control}
              rules={{ required: 'Date is required' }}
              render={({ field, fieldState }) => (
                <TextField
                  {...field}
                  label="Date"
                  type="date"
                  required
                  fullWidth
                  InputLabelProps={{ shrink: true }}
                  error={!!fieldState.error}
                  helperText={fieldState.error?.message}
                />
              )}
            />

            <Controller
              name="name"
              control={control}
              rules={{ required: 'Name is required' }}
              render={({ field, fieldState }) => (
                <TextField
                  {...field}
                  label="Holiday Name"
                  required
                  fullWidth
                  error={!!fieldState.error}
                  helperText={fieldState.error?.message}
                />
              )}
            />

            <Controller
              name="isOptional"
              control={control}
              render={({ field }) => (
                <FormControlLabel
                  control={
                    <Switch
                      checked={field.value}
                      onChange={(e) => field.onChange(e.target.checked)}
                    />
                  }
                  label="Optional holiday"
                />
              )}
            />
          </DialogContent>

          <DialogActions>
            <Button onClick={() => setAddOpen(false)}>Cancel</Button>
            <Button type="submit" variant="contained">Add</Button>
          </DialogActions>
        </form>
      </Dialog>

      {/* Import Summary Dialog */}
      <Dialog open={importOpen} onClose={() => setImportOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>CSV Import Summary</DialogTitle>
        <DialogContent>
          {importResult && (
            <>
              <Alert severity={importResult.errors.length === 0 ? 'success' : 'warning'} sx={{ mb: 2 }}>
                {importResult.imported} holidays imported successfully.
                {importResult.errors.length > 0 &&
                  ` ${importResult.errors.length} row(s) had errors.`}
              </Alert>

              {importResult.errors.length > 0 && (
                <List dense>
                  {importResult.errors.map((e, i) => (
                    <ListItem key={i}>
                      <ListItemText
                        primary={`Row ${e.row}`}
                        secondary={e.error}
                      />
                    </ListItem>
                  ))}
                </List>
              )}
            </>
          )}
        </DialogContent>
        <DialogActions>
          <Button variant="contained" onClick={() => setImportOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>

      {/* Toast */}
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
