import { describe, it, expect, vi, beforeEach } from 'vitest';
import { configureStore } from '@reduxjs/toolkit';
import notificationsReducer, {
  setNotifications,
  setUnreadCount,
  markItemRead,
  markAllItemsRead,
  fetchUnreadCount,
  fetchRecentNotifications,
  markReadThunk,
  markAllReadThunk,
  type NotificationItem,
} from '../store/notifications/notificationsSlice';

// ---------------------------------------------------------------------------
// Mock axiosClient so thunks don't make real HTTP calls
// ---------------------------------------------------------------------------

vi.mock('../api/axiosClient', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

import axiosClient from '../api/axiosClient';

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

function makeStore() {
  return configureStore({
    reducer: { notifications: notificationsReducer },
    middleware: (gm) => gm({ thunk: true }),
  });
}

function makeNotification(overrides: Partial<NotificationItem> = {}): NotificationItem {
  return {
    id: 'n-001',
    title: 'Leave Approved',
    body: 'Your leave from Aug 10 to Aug 12 has been approved.',
    isRead: false,
    resourceType: 'LeaveRequest',
    resourceId: 'lr-001',
    createdAt: new Date().toISOString(),
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// UT-FE-20 — Initial state
// ---------------------------------------------------------------------------

describe('UT-FE-20: notificationsSlice initial state', () => {
  it('initialises with empty items, zero unreadCount, and loading false', () => {
    const store = makeStore();
    const state = store.getState().notifications;
    expect(state.items).toEqual([]);
    expect(state.unreadCount).toBe(0);
    expect(state.loading).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// UT-FE-21 — setNotifications reducer
// ---------------------------------------------------------------------------

describe('UT-FE-21: setNotifications replaces items list', () => {
  it('replaces state.items with the dispatched payload', () => {
    const store = makeStore();
    const items = [makeNotification(), makeNotification({ id: 'n-002', title: 'Leave Rejected' })];
    store.dispatch(setNotifications(items));
    expect(store.getState().notifications.items).toEqual(items);
  });
});

// ---------------------------------------------------------------------------
// UT-FE-22 — setUnreadCount reducer
// ---------------------------------------------------------------------------

describe('UT-FE-22: setUnreadCount sets unreadCount', () => {
  it('sets unreadCount to the dispatched value', () => {
    const store = makeStore();
    store.dispatch(setUnreadCount(7));
    expect(store.getState().notifications.unreadCount).toBe(7);
  });
});

// ---------------------------------------------------------------------------
// UT-FE-23 — markItemRead reducer
// ---------------------------------------------------------------------------

describe('UT-FE-23: markItemRead marks item as read and decrements unreadCount', () => {
  it('sets item.isRead to true', () => {
    const store = makeStore();
    store.dispatch(setNotifications([makeNotification({ id: 'n-001', isRead: false })]));
    store.dispatch(setUnreadCount(1));
    store.dispatch(markItemRead('n-001'));
    expect(store.getState().notifications.items[0].isRead).toBe(true);
  });

  it('decrements unreadCount by 1', () => {
    const store = makeStore();
    store.dispatch(setUnreadCount(3));
    store.dispatch(setNotifications([makeNotification({ isRead: false })]));
    store.dispatch(markItemRead('n-001'));
    expect(store.getState().notifications.unreadCount).toBe(2);
  });

  it('does not go below 0 when unreadCount is already 0', () => {
    const store = makeStore();
    store.dispatch(setUnreadCount(0));
    store.dispatch(setNotifications([makeNotification({ isRead: false })]));
    store.dispatch(markItemRead('n-001'));
    expect(store.getState().notifications.unreadCount).toBe(0);
  });

  it('is a no-op for unknown notification IDs', () => {
    const store = makeStore();
    store.dispatch(setNotifications([makeNotification()]));
    store.dispatch(markItemRead('does-not-exist'));
    // items unchanged
    expect(store.getState().notifications.items[0].isRead).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// UT-FE-24 — markAllItemsRead reducer
// ---------------------------------------------------------------------------

describe('UT-FE-24: markAllItemsRead marks all items read and zeroes unreadCount', () => {
  it('sets isRead = true on all items', () => {
    const store = makeStore();
    store.dispatch(
      setNotifications([
        makeNotification({ id: 'n-001', isRead: false }),
        makeNotification({ id: 'n-002', isRead: false }),
      ]),
    );
    store.dispatch(setUnreadCount(2));
    store.dispatch(markAllItemsRead());

    const { items, unreadCount } = store.getState().notifications;
    expect(items.every((n) => n.isRead)).toBe(true);
    expect(unreadCount).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// UT-FE-25 — fetchUnreadCount thunk
// ---------------------------------------------------------------------------

describe('UT-FE-25: fetchUnreadCount thunk updates unreadCount on success', () => {
  it('sets unreadCount from API response', async () => {
    vi.mocked(axiosClient.get).mockResolvedValueOnce({
      data: { success: true, data: { count: 5 } },
    });
    const store = makeStore();
    await store.dispatch(fetchUnreadCount());
    expect(store.getState().notifications.unreadCount).toBe(5);
  });

  it('leaves unreadCount unchanged on API failure', async () => {
    vi.mocked(axiosClient.get).mockRejectedValueOnce(new Error('net'));
    const store = makeStore();
    store.dispatch(setUnreadCount(3));
    await store.dispatch(fetchUnreadCount());
    // Should not change from 3
    expect(store.getState().notifications.unreadCount).toBe(3);
  });
});

// ---------------------------------------------------------------------------
// UT-FE-26 — fetchRecentNotifications thunk
// ---------------------------------------------------------------------------

describe('UT-FE-26: fetchRecentNotifications thunk manages loading state and items', () => {
  it('sets loading=true while pending, then loading=false + items on success', async () => {
    const notifications = [makeNotification(), makeNotification({ id: 'n-002' })];
    vi.mocked(axiosClient.get).mockResolvedValueOnce({
      data: { success: true, data: notifications },
    });

    const store = makeStore();

    // Dispatch without awaiting to inspect pending state
    const promise = store.dispatch(fetchRecentNotifications());
    expect(store.getState().notifications.loading).toBe(true);

    await promise;
    expect(store.getState().notifications.loading).toBe(false);
    expect(store.getState().notifications.items).toHaveLength(2);
  });

  it('sets loading=false on rejection', async () => {
    vi.mocked(axiosClient.get).mockRejectedValueOnce(new Error('500'));
    const store = makeStore();
    await store.dispatch(fetchRecentNotifications());
    expect(store.getState().notifications.loading).toBe(false);
    expect(store.getState().notifications.items).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// UT-FE-27 — markReadThunk thunk
// ---------------------------------------------------------------------------

describe('UT-FE-27: markReadThunk marks individual item read via API', () => {
  it('calls POST /notifications/:id/read and marks item read in state', async () => {
    vi.mocked(axiosClient.post).mockResolvedValueOnce({ data: {} });

    const store = makeStore();
    store.dispatch(
      setNotifications([makeNotification({ id: 'n-001', isRead: false })]),
    );
    store.dispatch(setUnreadCount(1));

    await store.dispatch(markReadThunk('n-001'));

    expect(axiosClient.post).toHaveBeenCalledWith('/api/v1/notifications/n-001/read');
    expect(store.getState().notifications.items[0].isRead).toBe(true);
    expect(store.getState().notifications.unreadCount).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// UT-FE-28 — markAllReadThunk thunk
// ---------------------------------------------------------------------------

describe('UT-FE-28: markAllReadThunk marks all items read via API', () => {
  it('calls POST /notifications/read-all and clears unreadCount', async () => {
    vi.mocked(axiosClient.post).mockResolvedValueOnce({ data: {} });

    const store = makeStore();
    store.dispatch(
      setNotifications([
        makeNotification({ id: 'n-001', isRead: false }),
        makeNotification({ id: 'n-002', isRead: false }),
      ]),
    );
    store.dispatch(setUnreadCount(2));

    await store.dispatch(markAllReadThunk());

    expect(axiosClient.post).toHaveBeenCalledWith('/api/v1/notifications/read-all');
    expect(store.getState().notifications.unreadCount).toBe(0);
    expect(store.getState().notifications.items.every((n) => n.isRead)).toBe(true);
  });
});
