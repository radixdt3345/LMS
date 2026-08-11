import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDispatch, useSelector } from 'react-redux';
import {
  IconButton,
  Badge,
  Popover,
  Box,
  Typography,
  List,
  ListItem,
  ListItemButton,
  Button,
  CircularProgress,
} from '@mui/material';
import NotificationsIcon from '@mui/icons-material/Notifications';
import type { RootState, AppDispatch } from '../store';
import {
  fetchUnreadCount,
  fetchRecentNotifications,
  markReadThunk,
  markAllReadThunk,
} from '../store/notifications/notificationsSlice';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Returns a human-readable relative timestamp string (e.g. "2 hours ago").
 * Pure calculation — no external library.
 */
function relativeTime(isoString: string): string {
  const diffMs = Date.now() - new Date(isoString).getTime();
  const diffSec = Math.floor(diffMs / 1000);
  if (diffSec < 60) return 'just now';
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return `${diffMin} minute${diffMin !== 1 ? 's' : ''} ago`;
  const diffHour = Math.floor(diffMin / 60);
  if (diffHour < 24) return `${diffHour} hour${diffHour !== 1 ? 's' : ''} ago`;
  const diffDay = Math.floor(diffHour / 24);
  return `${diffDay} day${diffDay !== 1 ? 's' : ''} ago`;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

/**
 * NotificationBell — renders in the AppBar on all authenticated routes.
 *
 * Behaviour:
 * - Polls GET /api/v1/notifications/unread-count every 60 seconds.
 * - On click, fetches the 10 most recent notifications and opens a Popover.
 * - Each item click marks it read and navigates to its resource URL if applicable.
 * - "Mark all read" button marks every item read server-side and updates state.
 */
export default function NotificationBell() {
  const dispatch = useDispatch<AppDispatch>();
  const navigate = useNavigate();

  const { items, unreadCount, loading } = useSelector(
    (state: RootState) => state.notifications,
  );

  const [anchorEl, setAnchorEl] = useState<HTMLButtonElement | null>(null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Initial fetch + 60-second polling interval.
  useEffect(() => {
    void dispatch(fetchUnreadCount());

    intervalRef.current = setInterval(() => {
      void dispatch(fetchUnreadCount());
    }, 60_000);

    return () => {
      if (intervalRef.current !== null) {
        clearInterval(intervalRef.current);
      }
    };
  }, [dispatch]);

  const handleOpen = (event: React.MouseEvent<HTMLButtonElement>) => {
    setAnchorEl(event.currentTarget);
    void dispatch(fetchRecentNotifications());
  };

  const handleClose = () => {
    setAnchorEl(null);
  };

  const handleItemClick = (id: string, resourceType: string | null) => {
    void dispatch(markReadThunk(id));
    if (resourceType === 'LeaveRequest') {
      navigate('/leaves/history');
    }
    handleClose();
  };

  const handleMarkAll = () => {
    void dispatch(markAllReadThunk());
  };

  const open = Boolean(anchorEl);

  return (
    <>
      <IconButton
        color="inherit"
        onClick={handleOpen}
        aria-label="notifications"
        aria-haspopup="true"
        aria-expanded={open ? 'true' : undefined}
      >
        <Badge
          badgeContent={unreadCount}
          color="error"
          invisible={unreadCount === 0}
        >
          <NotificationsIcon />
        </Badge>
      </IconButton>

      <Popover
        open={open}
        anchorEl={anchorEl}
        onClose={handleClose}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        PaperProps={{ sx: { width: 360, maxHeight: 480 } }}
      >
        {/* Header row */}
        <Box
          display="flex"
          alignItems="center"
          justifyContent="space-between"
          px={2}
          py={1}
          borderBottom="1px solid"
          borderColor="divider"
        >
          <Typography variant="subtitle1" fontWeight={600}>
            Notifications
          </Typography>
          <Button
            size="small"
            onClick={handleMarkAll}
            disabled={unreadCount === 0}
          >
            Mark all read
          </Button>
        </Box>

        {/* Body */}
        {loading ? (
          <Box display="flex" justifyContent="center" py={3}>
            <CircularProgress size={24} />
          </Box>
        ) : items.length === 0 ? (
          <Box py={3} textAlign="center">
            <Typography color="text.secondary" variant="body2">
              No notifications
            </Typography>
          </Box>
        ) : (
          <List disablePadding>
            {items.map((n) => (
              <ListItem key={n.id} disablePadding divider>
                <ListItemButton
                  onClick={() => handleItemClick(n.id, n.resourceType)}
                  sx={{ py: 1.5, px: 2, alignItems: 'flex-start' }}
                >
                  <Box>
                    <Typography
                      variant="body2"
                      fontWeight={n.isRead ? 400 : 700}
                    >
                      {n.title}
                    </Typography>
                    <Typography
                      variant="caption"
                      color="text.secondary"
                      display="block"
                    >
                      {n.body.length > 60
                        ? `${n.body.slice(0, 60)}…`
                        : n.body}
                    </Typography>
                    <Typography
                      variant="caption"
                      color="text.disabled"
                      display="block"
                      mt={0.5}
                    >
                      {relativeTime(n.createdAt)}
                    </Typography>
                  </Box>
                </ListItemButton>
              </ListItem>
            ))}
          </List>
        )}
      </Popover>
    </>
  );
}
