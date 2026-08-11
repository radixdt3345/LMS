import { createSlice, createAsyncThunk, type PayloadAction } from '@reduxjs/toolkit';
import axiosClient from '../../api/axiosClient';

export interface NotificationItem {
  id: string;
  title: string;
  body: string;
  isRead: boolean;
  resourceType: string | null;
  resourceId: string | null;
  createdAt: string;
}

interface NotificationsState {
  items: NotificationItem[];
  unreadCount: number;
  loading: boolean;
}

interface ApiResponse<T> { success: boolean; data: T; }
interface UnreadCountResponse { count: number; }

const initialState: NotificationsState = { items: [], unreadCount: 0, loading: false };

export const fetchUnreadCount = createAsyncThunk('notifications/fetchUnreadCount', async () => {
  const response = await axiosClient.get<ApiResponse<UnreadCountResponse>>('/api/v1/notifications/unread-count');
  return response.data.data.count;
});

export const fetchRecentNotifications = createAsyncThunk('notifications/fetchRecentNotifications', async () => {
  const response = await axiosClient.get<ApiResponse<NotificationItem[]>>('/api/v1/notifications', { params: { page: 1, limit: 10 } });
  return response.data.data;
});

export const markReadThunk = createAsyncThunk('notifications/markRead', async (id: string) => {
  await axiosClient.post(`/api/v1/notifications/${id}/read`);
  return id;
});

export const markAllReadThunk = createAsyncThunk('notifications/markAllRead', async () => {
  await axiosClient.post('/api/v1/notifications/read-all');
});

const notificationsSlice = createSlice({
  name: 'notifications',
  initialState,
  reducers: {
    setNotifications: (state, action: PayloadAction<NotificationItem[]>) => { state.items = action.payload; },
    setUnreadCount: (state, action: PayloadAction<number>) => { state.unreadCount = action.payload; },
    markItemRead: (state, action: PayloadAction<string>) => {
      const item = state.items.find((n) => n.id === action.payload);
      if (item) item.isRead = true;
      if (state.unreadCount > 0) state.unreadCount -= 1;
    },
    markAllItemsRead: (state) => { state.items.forEach((n) => { n.isRead = true; }); state.unreadCount = 0; },
  },
  extraReducers: (builder) => {
    builder
      .addCase(fetchUnreadCount.fulfilled, (state, action) => { state.unreadCount = action.payload; })
      .addCase(fetchRecentNotifications.pending, (state) => { state.loading = true; })
      .addCase(fetchRecentNotifications.fulfilled, (state, action) => { state.loading = false; state.items = action.payload; })
      .addCase(fetchRecentNotifications.rejected, (state) => { state.loading = false; })
      .addCase(markReadThunk.fulfilled, (state, action) => {
        const item = state.items.find((n) => n.id === action.payload);
        if (item && !item.isRead) { item.isRead = true; if (state.unreadCount > 0) state.unreadCount -= 1; }
      })
      .addCase(markAllReadThunk.fulfilled, (state) => { state.items.forEach((n) => { n.isRead = true; }); state.unreadCount = 0; });
  },
});

export const { setNotifications, setUnreadCount, markItemRead, markAllItemsRead } = notificationsSlice.actions;
export default notificationsSlice.reducer;
