import axios, { type AxiosInstance, type InternalAxiosRequestConfig, type AxiosResponse } from 'axios';
import { store } from '../store';
import { logout, tokenRefreshSuccess } from '../store/slices/authSlice';

const BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? 'http://localhost:5000';

export const authApi: AxiosInstance = axios.create({ baseURL: BASE_URL });
export const apiClient: AxiosInstance = axios.create({ baseURL: BASE_URL });

let isRefreshing = false;
let pendingQueue: Array<{ resolve: (token: string) => void; reject: (err: unknown) => void }> = [];

function drainQueue(error: unknown, token: string | null): void {
  pendingQueue.forEach(({ resolve, reject }) => { error != null ? reject(error) : resolve(token!); });
  pendingQueue = [];
}

apiClient.interceptors.request.use((config: InternalAxiosRequestConfig): InternalAxiosRequestConfig => {
  const { accessToken } = store.getState().auth;
  if (accessToken != null && config.headers != null) config.headers.Authorization = `Bearer ${accessToken}`;
  return config;
});

type RetriableConfig = InternalAxiosRequestConfig & { _retried?: boolean };

apiClient.interceptors.response.use(
  (response: AxiosResponse): AxiosResponse => response,
  async (error: unknown): Promise<AxiosResponse> => {
    const axiosError = error as { config?: RetriableConfig; response?: { status: number } };
    const status = axiosError.response?.status;
    const config = axiosError.config;
    if (status !== 401 || config == null || config._retried === true) return Promise.reject(error);
    config._retried = true;
    if (isRefreshing) {
      return new Promise<string>((resolve, reject) => { pendingQueue.push({ resolve, reject }); })
        .then(newToken => { if (config.headers != null) config.headers.Authorization = `Bearer ${newToken}`; return apiClient(config); });
    }
    isRefreshing = true;
    const { refreshToken } = store.getState().auth;
    if (refreshToken == null) {
      store.dispatch(logout()); drainQueue(new Error('No refresh token'), null); isRefreshing = false;
      return Promise.reject(error);
    }
    try {
      const { data } = await authApi.post<{ data: { accessToken: string; refreshToken: string } }>('/api/v1/auth/refresh', { refreshToken });
      store.dispatch(tokenRefreshSuccess({ accessToken: data.data.accessToken, refreshToken: data.data.refreshToken }));
      drainQueue(null, data.data.accessToken);
      if (config.headers != null) config.headers.Authorization = `Bearer ${data.data.accessToken}`;
      return apiClient(config);
    } catch (refreshError) {
      store.dispatch(logout()); drainQueue(refreshError, null); return Promise.reject(refreshError);
    } finally { isRefreshing = false; }
  }
);

export default apiClient;
