import { configureStore } from '@reduxjs/toolkit';
import createSagaMiddleware from 'redux-saga';
import { all } from 'redux-saga/effects';
import authReducer from '../auth/authSlice';
import { rootAuthSaga } from '../auth/authSaga';
import notificationsReducer from './notifications/notificationsSlice';

function* rootSaga() {
  yield all([rootAuthSaga()]);
}

const sagaMiddleware = createSagaMiddleware();

export const store = configureStore({
  reducer: {
    auth: authReducer,
    notifications: notificationsReducer,
  },
  // Thunk middleware is enabled (default) alongside saga middleware.
  // Notifications uses createAsyncThunk; auth saga actions use redux-saga.
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware().concat(sagaMiddleware),
});

sagaMiddleware.run(rootSaga);

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
