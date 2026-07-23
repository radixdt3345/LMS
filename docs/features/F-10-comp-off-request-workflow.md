# F-10 — Comp-Off Request Workflow

## Purpose
Allow Employees and Managers to submit comp-off requests for work done on weekends or public holidays. Validates worked hours (≥4h for half-day, ≥8h for full-day), routes to manager or HR Admin for approval, credits comp-off balance on approval with a 30-day expiry, and prevents post-submit cancellation by employees.

## User Stories

### US-10.1: Submit Comp-Off Request
As an Employee or Manager, I want to submit a comp-off request for a day I worked on a weekend or holiday so that I earn compensatory leave credited to my balance.

**Acceptance Criteria:**
- AC-40: Non-weekend/holiday date → 422.
- AC-41: Worked hours < 4 (e.g., 3h) → 422 "Insufficient worked hours."
- AC-42: is_half_day = true, worked hours = 5 (> 4h) → 201.
- AC-43: is_half_day = false, worked hours = 10 (≥ 8h) → 201.
- FR-54: Cannot cancel once submitted.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| Employee | Submit comp-off for self | Cancel after submit |
| Manager | Submit comp-off for self | Cancel after submit |
| HR Admin | — | Submit comp-off (out of scope) |

### US-10.2: Approve or Reject Comp-Off Request
As a Manager or HR Admin (for no-manager employees), I want to approve or reject comp-off requests so that eligible compensatory credits are correctly granted.

**Acceptance Criteria:**
- FR-55: Rejection requires mandatory reason.
- FR-56: Approval credits 0.5 (half-day) or 1.0 (full-day) to comp-off balance with expiry = earn_date + 30 days.
- AC-44 (analogous): No-manager employee's comp-off routes to HR Admin.
- FR-57: All comp-off events recorded in Audit Trail.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| Manager | Approve/reject direct reports' comp-off | Approve for non-direct-reports |
| HR Admin | Approve/reject for no-manager employees; view all | — |
| Employee | View own status | Approve/reject |

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-51 | Comp-off submission fields | MUST |
| FR-52 | Date must be weekend or holiday | MUST |
| FR-53 | Worked hours: half-day > 4h, full-day ≥ 8h; < 4h blocked | MUST |
| FR-54 | No cancellation after submit | MUST |
| FR-55 | Approved/rejected by manager or HR Admin; rejection needs reason | MUST |
| FR-56 | Approval credits balance with expiry date | MUST |
| FR-57 | Audit trail for all comp-off events | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| PT-8 | Comp-off submit → manager approve → balance credited | Employee, Manager |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| comp_off_requests | Owner + Manager (team) + HR/SA | Employee, Manager (own) | — |
| comp_off_credits | Owner + HR/SA | CompOffCreditService (on approval) | — (expired via job) |
| leave_balances | (see F-07) | LeaveBalanceService (credit on approval) | — |
| notifications | — | NotificationService | — |
| audit_logs | — | AuditService | — |

## Integration Points
- F-07 (Leave Balance): comp-off credit on approval
- F-08 (Holiday Calendar): date must be weekend or holiday
- F-11 (Notifications): email + in-app on submit/approve/reject
- F-10 Approval Engine (F-11): single-level approval (L1 only, no L2 for comp-off)

## HITL Flag
NO

## Execution Wave
Wave 2: Core — depends on employees, holidays, balance management.

## Dependencies
Depends on: F-05 (employees), F-07 (leave balance), F-08 (holiday calendar)
Blocks: F-11 (Notifications — comp-off events)
