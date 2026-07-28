import { all, fork } from 'redux-saga/effects'
import { authSaga } from './authSaga'

export function* rootSaga(): Generator {
  yield all([fork(authSaga)])
}
