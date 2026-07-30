import {
  Card,
  CardContent,
  Typography,
  LinearProgress,
  Box,
  Chip,
} from '@mui/material';
import { AccrualType, BalanceItem } from '../api/leaveBalanceApi';

interface Props {
  balance: BalanceItem;
}

/**
 * Displays leave balance details for a single leave type.
 * - Annual / OneTime: shows allocated, used, remaining with a LinearProgress bar.
 * - Unlimited (e.g. Unpaid Leave): hides the progress bar and shows an info chip.
 */
export default function LeaveBalanceCard({ balance }: Props) {
  const isUnlimited = balance.accrualType === AccrualType.Unlimited;

  const usedPercent = isUnlimited
    ? 0
    : balance.allocatedDays > 0
    ? Math.min(100, (balance.usedDays / balance.allocatedDays) * 100)
    : 0;

  return (
    <Card variant="outlined" sx={{ height: '100%' }}>
      <CardContent>
        <Box display="flex" justifyContent="space-between" alignItems="flex-start" mb={1}>
          <Typography variant="subtitle1" fontWeight={600}>
            {balance.leaveTypeName}
          </Typography>
          {isUnlimited && (
            <Chip label="Unlimited" size="small" color="info" />
          )}
        </Box>

        {isUnlimited ? (
          <Typography variant="body2" color="text.secondary">
            No balance limit applies to this leave type.
          </Typography>
        ) : (
          <>
            <Box display="flex" justifyContent="space-between" mb={0.5}>
              <Typography variant="body2" color="text.secondary">
                Used: <strong>{balance.usedDays}</strong>
              </Typography>
              <Typography variant="body2" color="text.secondary">
                Remaining: <strong>{balance.availableDays}</strong>
              </Typography>
            </Box>
            <LinearProgress
              variant="determinate"
              value={usedPercent}
              sx={{ height: 6, borderRadius: 3, mb: 1 }}
            />
            <Typography variant="caption" color="text.secondary">
              Allocated: {balance.allocatedDays} days
            </Typography>
          </>
        )}
      </CardContent>
    </Card>
  );
}
