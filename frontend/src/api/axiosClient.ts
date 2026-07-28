import axios, {
  type AxiosInstance,
  type InternalAxiosRequestConfig,
  type AxiosResponse,
} from 'axios'
import { store } from '../store'
import { logout, tokenRefreshSuccess } from '../store/slices/authSlice'

const BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ??
  'http://localhost:5000'

// Separate bare instance for auth calls (login, refresh).
// No JWT interceptor — avoids redirect loops.
export const authApi: AxiosInstance = axios.create({ baseURL: BASE_URL })

// Main API client — all authenticated calls use this instance.
export const apiClient: AxiosInstance = axios.create({ baseURL: BASE_URL })

let isRefreshing = false
let pendingQueue: Array<{
  resolve: (token: string) => void
  reject: (err: unknown) => void
}> = []

function drainQueue(error: unknown, token: string | null): void {
  pendingQueue.forEach(({ resolve, reject }) => {
    if (error != null) {
      reject(error)
    } else {
      resolve(token!)
    }
  })
  pendingQueue = []
}

// REQUEST interceptor: attach Bearer token from Redux state.
// NEVER reads from localStorage or sessionStorage.
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig): InternalAxiosRequestConfig => {
    const { accessToken } = store.getState().auth
    if (accessToken != null && config.headers != null) {
      config.headers.Authorization = `Bearer ${accessToken}`
    }
    return config
  }
)

type RetriableConfig = InternalAxiosRequestConfig & { _retried?: boolean }

// RESPONSE interceptor: on 401 attempt one silent refresh, then retry.
// On second 401 or missing refresh token, dispatch logout().
apiClient.interceptors.response.use(
  (response: AxiosResponse): AxiosResponse => response,
  async (error: unknown): Promise<AxiosResponse> => {
    const axiosError = error as {
      config?: RetriableConfig
      response?: { status: number }
    }

    const status = axiosError.response?.status
    const config = axiosError.config

    // Only handle 401; don't retry if already retried once
    if (status !== 401 || config == null || config._retried === true) {
      return Promise.reject(error)
    }

    config._retried = true

    // If a refresh is already in flight, queue this request
    if (isRefreshing) {
      return new Promise<string>((resolve, reject) => {
        pendingQueue.push({ resolve, reject })
      }).then((newToken) => {
        if (config.headers != null) {
          config.headers.Authorization = `Bearer ${newToken}`
        }
        return apiClient(config)
      })
    }

    isRefreshing = true

    const { refreshToken } = store.getState().auth

    if (refreshToken == null) {
      store.dispatch(logout())
      drainQueue(new Error('No refresh token available'), null)
      isRefreshing = false
      return Promise.reject(error)
    }

    try {
      const { data } = await authApi.post<{
        data: { accessToken: string; refreshToken: string }
      }>('/api/v1/auth/refresh', { refreshToken })

      const newAccessToken = data.data.accessToken
      const newRefreshToken = data.data.refreshToken

      // Store refreshed tokens in Redux memory only
      store.dispatch(
        tokenRefreshSuccess({
          accessToken: newAccessToken,
          refreshToken: newRefreshToken,
        })
      )

      drainQueue(null, newAccessToken)

      if (config.headers != null) {
        config.headers.Authorization = `Bearer ${newAccessToken}`
      }
      return apiClient(config)
    } catch (refreshError) {
      store.dispatch(logout())
      drainQueue(refreshError, null)
      return Promise.reject(refreshError)
    } finally {
      isRefreshing = false
    }
  }
)
