import { call, put, select, takeLatest } from 'redux-saga/effects'
import type { RootState } from '../index'
import {
  loginRequest,
  loginSuccess,
  loginFailure,
  tokenRefreshSuccess,
  logout,
  type AuthTokens,
} from '../slices/authSlice'
import { authApi } from '../../api/axiosClient'

interface LoginApiResponse {
  data: {
    accessToken: string
    refreshToken: string
    userId: string
    email: string
    role: string
  }
}

interface RefreshApiResponse {
  data: {
    accessToken: string
    refreshToken: string
  }
}

function* handleLogin(
  action: ReturnType<typeof loginRequest>
): Generator<unknown, void, LoginApiResponse> {
  try {
    const response = yield call(
      [authApi, authApi.post],
      '/api/v1/auth/login',
      action.payload
    )

    const tokens: AuthTokens = {
      accessToken: response.data.accessToken,
      refreshToken: response.data.refreshToken,
      userId: response.data.userId,
      email: response.data.email,
      role: response.data.role,
    }
    yield put(loginSuccess(tokens))
  } catch (error: unknown) {
    const message =
      error instanceof Error
        ? error.message
        : 'Login failed. Please try again.'
    yield put(loginFailure(message))
  }
}

// Exported for use by axiosClient interceptor via saga channel if needed
export function* attemptTokenRefresh(): Generator<unknown, boolean, unknown> {
  const refreshToken = (yield select(
    (state: RootState) => state.auth.refreshToken
  )) as string | null

  if (!refreshToken) {
    yield put(logout())
    return false
  }

  try {
    const response = (yield call(
      [authApi, authApi.post],
      '/api/v1/auth/refresh',
      { refreshToken }
    )) as RefreshApiResponse

    yield put(
      tokenRefreshSuccess({
        accessToken: response.data.accessToken,
        refreshToken: response.data.refreshToken,
      })
    )
    return true
  } catch {
    yield put(logout())
    return false
  }
}

export function* authSaga(): Generator {
  yield takeLatest(loginRequest.type, handleLogin)
}
