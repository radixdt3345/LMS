import { all, fork } from 'redux-saga/effects';
import { authSaga } from './authSaga';
import { dashboardSaga } from './dashboardSaga';

export function* rootSaga(): Generator {
  yield all([fork(authSaga), fork(dashboardSaga)]);
}
