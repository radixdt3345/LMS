import { Alert, AlertTitle } from '@mui/material';
import { CompOffCredit } from '../api/leaveBalanceApi';

interface Props {
  credits: CompOffCredit[];
}

/** Days ahead of expiry at which the warning fires. */
const WARN_DAYS = 30;

/**
 * Renders an MUI Alert listing comp-off credits that expire within WARN_DAYS days.
 * Renders nothing when there are no expiring credits.
 */
export default function CompOffExpiryWarning({ credits }: Props) {
  const today  = new Date();
  const cutoff = new Date(today);
  cutoff.setDate(today.getDate() + WARN_DAYS);

  const expiringSoon = credits.filter((c) => {
    const exp = new Date(c.expiresAt);
    return exp <= cutoff && c.usedDays < c.creditDays;
  });

  if (expiringSoon.length === 0) return null;

  return (
    <Alert severity="warning" sx={{ mb: 2 }}>
      <AlertTitle>Comp-off credits expiring soon</AlertTitle>
      {expiringSoon.map((c) => (
        <div key={c.id}>
          {c.creditDays - c.usedDays} day(s) expire on{' '}
          {new Date(c.expiresAt).toLocaleDateString('en-IN', {
            day:   'numeric',
            month: 'short',
            year:  'numeric',
          })}
        </div>
      ))}
    </Alert>
  );
}
