# F-07 — Leave Balance Management

## Purpose
Maintain per-employee, per-leave-type balance records. Handles annual credit (Jan 1 lump-sum), mid-year joiner proration, real-time deduction/restoration on approval/cancel/revoke, comp-off credit lifecycle, and year-end lapse via Hangfire background jobs.

## User Stories

### US-07.1: Employee Views Leave Balances
As an Employee, I want to see my current leave balances per leave type so that I can make informed leave decisions.

**Acceptance Criteria:**
- AC-21: GET /api/v1/balances/me returns HTTP 200 with array of balance records (total_entitled, used, balance per active leave type).
- AC-24: Mid-year joiner (July 1, 184 remaining days) with 12-day Casual Leave receives 6.0 days.
- AC-57: Balance restored after leave cancellation reflects +1.0 (full) or +0.5 (half-day).

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| Employee | View own balances | View other employees' balances |
| Manager | View own balances + direct reports' balances | — |
| HR Admin / Super Admin | View any employee's balances | — |

### US-07.2: Balance Deduction and Restoration
As the system, I want to automatically deduct balances on leave approval and restore them on cancellation or revocation so that balance records are always accurate.

**Acceptance Criteria:**
- AC-22: Full-day approval deducts exactly 1.0 day.
- AC-23: Half-day approval deducts exactly 0.5 days.
- AC-27: Zero balance → HTTP 422 "Insufficient balance" on submit.
- AC-28: Unpaid Leave exempt from balance check → HTTP 200 even with zero balance.
- FR-37: Would-go-negative → InsufficientBalanceException.

**RBAC:** System behavior — no direct user action.

### US-07.3: Comp-Off Balance and Expiry
As the system, I want comp-off credits to be tracked with earn date and expiry (earn + 30 days) and expired automatically by a daily Hangfire job so that comp-off balance is always accurate.

**Acceptance Criteria:**
- AC-25: Comp-off credit earned July 1 → expiry_date = July 31.
- AC-26: Daily CompOffExpiryJob sets CompOffCredit.status = expired and decrements LeaveBalance.balance for credits past expiry.
- FR-34: LeaveBalance is single source of truth; CompOffCredit stores earn/expiry for audit.

**RBAC:** System behavior (Hangfire job).

### US-07.4: Year-End Lapse and New-Year Credit
As the system, I want to lapse all unused leave balances on December 31 and credit new annual entitlements on January 1 so that the leave year resets cleanly.

**Acceptance Criteria:**
- FR-38: Dec 31 Hangfire job zeros all balances; each lapse logged in audit trail.
- FR-33: Jan 1 Hangfire job credits AnnualLeaveDays per leave type per employee.
- Mid-year joiners receive prorated credit on their date_of_joining (handled by NewYearCreditJob or employee creation flow).

**RBAC:** System behavior (Hangfire jobs).

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-31 | Per-employee, per-leave-type balance records | MUST |
| FR-32 | Full-day = 1.0 deduction; half-day = 0.5 | MUST |
| FR-33 | Annual credit + mid-year proration | MUST |
| FR-34 | Comp-off credit with earn/expiry tracking | MUST |
| FR-35 | Comp-off expiry = earn_date + 30 days | MUST |
| FR-36 | Daily Hangfire job expires comp-off | MUST |
| FR-37 | Insufficient balance blocks submit (except Unpaid) | MUST |
| FR-38 | Dec 31 year-end lapse via Hangfire | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| PT-5 | Employee with zero balance attempts leave → blocked | Employee |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| leave_balances | Employee (own), Manager (team), HR/SA (all) | LeaveBalanceService, Hangfire jobs | — |
| comp_off_credits | Employee (own), Manager (team), HR/SA (all) | CompOffCreditService, Hangfire job | — (status=expired) |
| audit_logs | — | AuditService (lapse events) | — |

## Integration Points
- Hangfire jobs: CompOffExpiryJob (daily), YearEndLapseJob (Dec 31), NewYearCreditJob (Jan 1)
- F-08 (Leave Requests): deduction on approval, restoration on cancel/revoke
- F-09 (Comp-off Requests): credit on comp-off approval

## HITL Flag
NO

## Execution Wave
Wave 1: Foundation — must exist before leave requests can be submitted or validated.

## Dependencies
Depends on: F-05 (employees), F-06 (leave types)
Blocks: F-08 (Leave Requests — balance check), F-09 (Comp-off — balance credit)
