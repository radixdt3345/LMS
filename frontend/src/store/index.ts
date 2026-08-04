import { configureStore } from '@reduxjs/toolkit';
import createSagaMiddleware from 'redux-saga';
import { all, fork } from 'redux-saga/effects';
import authReducer from '../auth/authSlice';
import { rootAuthSaga } from '../auth/authSaga';
import notificationsReducer from './notifications/notificationsSlice';
import { dashboardReducer } from './slices/dashboardSlice';
import { dashboardSaga } from './sagas/dashboardSaga';

function* rootSaga() {
  yield all([
    rootAuthSaga(),
    fork(dashboardSaga),
  ]);
}

const sagaMiddleware = createSagaMiddleware();

export const store = configureStore({
  reducer: {
    auth: authReducer,
    notifications: notificationsReducer,
    dashboard: dashboardReducer,
  },
  // Thunk middleware is enabled (default) alongside saga middleware.
  // Notifications uses createAsyncThunk; auth saga actions use redux-saga.
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware().concat(sagaMiddleware),
});

sagaMiddleware.run(rootSaga);

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
