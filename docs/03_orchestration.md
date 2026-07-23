# Orchestration & Workflow — Leave Management System (LMS)

## Key Workflows

### 1. Employee Leave Application Flow
```
Employee submits leave request
  → Validate: balance sufficient, no overlap, team overlap limit, no weekend/holiday start/end
  → Compute sandwich rule days count
  → Save as Pending L1 Approval
  → Email notification to reporting manager (or HR Admin if no manager)
  → Manager approves L1
    → Check L2 conditions (duration > 3d, RequiresHRFlag, retroactive)
      → [L2 needed] → Pending L2 Approval → HR Admin approves → Approved
      → [No L2 needed] → Approved
  → On Approved: deduct balance, send email to employee, trigger Google Calendar sync
```

### 2. No-Manager Routing
```
Employee has no reporting_manager_id
  → All leave/comp-off notifications → HR Admin
  → L1 approver = HR Admin
  → When HR Admin approves as L1 → L2 is automatically SKIPPED
  → Single HR Admin approval = Final approval
```

### 3. Comp-Off Request Flow
```
Employee submits comp-off request (date must be weekend/holiday)
  → Validate worked hours (>4h → half-day; ≥8h → full-day; <4h → blocked)
  → Route to reporting manager (or HR Admin if no manager)
  → Approver approves → credit comp-off balance (0.5 or 1 day) in LeaveBalance
  → Create CompOffCredit record with expiry = earn_date + 30 days
  → Email employee: approved/rejected
```

### 4. Approval Escalation Flow (Hangfire — daily)
```
EscalationJob runs daily:
  → Query all Pending L1 + Pending L2 leave requests
  → Query all pending comp-off requests
  → For each: if (now - last_reminder_sent) >= 2 days → send reminder email to pending approver
  → Update last_reminder_sent timestamp
```

### 5. Google Calendar Sync Flow (Hangfire)
```
LeaveApproved event fired
  → Enqueue CalendarSyncJob in Hangfire
  → CalendarSyncJob: check user has authorized Google Calendar (OAuth2 consent)
    → If authorized: create all-day event via Google Calendar API v3
    → If not authorized: log "calendar sync skipped — no OAuth consent"
  → Retry 3x on failure (exponential backoff)
  → After 3 failures: log "calendar sync failed", mark notification.calendar_status = failed
  → Do NOT block or reverse the leave approval

LeaveCancelled / LeaveRevoked event fired
  → Enqueue CalendarDeleteJob in Hangfire
  → Delete the previously-created calendar event (if exists)
  → Same retry + failure-logging pattern
```

### 6. Year-End Balance Lapse Flow (Hangfire — Dec 31)
```
YearEndLapseJob runs on Dec 31:
  → Query all active LeaveBalances where balance > 0 and year = current year
  → For each: set used = total_entitled (effectively zeroing remaining balance)
  → Log lapse in AuditLog (action = BALANCE_LAPSED, old_value = {balance}, new_value = {0})
  → Jan 1: CreditBalancesJob runs → create new LeaveBalance rows for new year with prorated/full entitlements
```

### 7. Comp-Off Expiry Flow (Hangfire — daily)
```
CompOffExpiryJob runs daily:
  → Query CompOffCredit where expiry_date <= today AND status = active
  → For each expired credit:
    → Decrement LeaveBalance.balance by the credit's days
    → Set CompOffCredit.status = expired
    → Log in AuditLog
```

### 8. JWT Token Refresh Flow
```
Frontend: Axios 401 interceptor fires
  → POST /api/v1/auth/refresh with HttpOnly cookie (refresh token)
  → Backend: validate refresh token in DB (not invalidated, not expired)
  → Issue new JWT access token (24h)
  → Return new token in response body
  → Frontend: store new token in memory, retry original request
```

## Inter-Service Communication Patterns

| Pattern | Where Used |
|---------|-----------|
| Request-Response (REST) | All API endpoints — synchronous |
| Fire-and-Forget (Hangfire) | Email dispatch, Google Calendar sync, audit logging of async events |
| Polling (Frontend) | Notification unread count: every 60 seconds via GET /api/v1/notifications |
| Callback (OAuth2) | Azure AD SSO callback at GET /api/v1/auth/sso/callback |
| Callback (OAuth2) | Google Calendar per-user OAuth2 consent flow |

## Event-Driven vs Request-Response Boundaries

### Synchronous (Request-Response)
- All CRUD operations (employee, department, leave type, holiday)
- Leave request submission, cancel, revoke
- Comp-off request submission
- L1/L2 approval/rejection
- Balance reads
- Audit log search

### Asynchronous (Hangfire Jobs)
- Email delivery (SendGrid) — queued on any status-changing event
- Google Calendar event create/delete — queued on leave approval/cancel/revoke
- Escalation reminders — daily recurring job
- Comp-off expiry — daily recurring job
- Year-end balance lapse — yearly recurring job (Dec 31)
- New-year balance credit — yearly recurring job (Jan 1)

## Background Job Patterns

| Job | Trigger | Schedule |
|-----|---------|---------|
| EscalationJob | Recurring | Daily (e.g. 08:00 IST) |
| CompOffExpiryJob | Recurring | Daily (e.g. 01:00 IST) |
| YearEndLapseJob | Recurring | Dec 31 23:59 IST |
| NewYearCreditJob | Recurring | Jan 1 00:01 IST |
| EmailDispatchJob | Enqueued on event | Immediate + retry 5x |
| CalendarSyncJob | Enqueued on event | Immediate + retry 3x |
| CalendarDeleteJob | Enqueued on event | Immediate + retry 3x |

All Hangfire jobs use PostgreSQL as persistence store. The Hangfire dashboard is available at `/hangfire` (Super Admin only in Phase 1).
