# F-09 — Leave Request Workflow

## Purpose
The core leave lifecycle: Employees and Managers apply for leave (draft or direct submit), the system enforces all validation rules (balance, overlap, sandwich rule, team limit, working days, half-day conflict), manages the status state machine from Draft to Completed, and handles cancellation and HR revocation with balance restoration.

## User Stories

### US-09.1: Apply for Leave
As an Employee or Manager, I want to apply for leave by selecting leave type, dates, half-day option, reason, and attachment — and have the system validate my request — so that only valid leave requests are submitted.

**Acceptance Criteria:**
- AC-27: Zero balance → 422 "Insufficient balance."
- AC-28: Unpaid Leave → 200 (balance exempt).
- AC-29: Weekend start date → 422.
- AC-30: Holiday start date → 422.
- AC-31: Overlapping approved leave → 422.
- AC-32: Isolated holiday (Mon leave + Tue holiday + Wed leave) → days_count = 2.
- AC-33: Chained (Thu holiday + Fri leave + Sat + Sun) → days_count = 4.
- AC-34: Team overlap limit reached → 422.
- AC-35: Duplicate half-day for same date → 422 with modify-to-full-day prompt.
- AC-51: Calendar disables weekends and holidays as start/end dates.
- FR-40: Request can be saved as Draft before submission.
- FR-49: Attachment: PDF/JPG/PNG only, max 5MB, stored on local filesystem.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| Employee | Apply/draft leave for self | Apply for other employees |
| Manager | Apply/draft leave for self | Apply for direct reports |
| HR Admin | — | Apply for leave (out of scope FR-2/Section 2) |
| Super Admin | — | Apply for leave (out of scope) |

### US-09.2: Cancel Leave
As an Employee or Manager, I want to cancel a submitted or approved leave request before it starts so that my leave balance is restored.

**Acceptance Criteria:**
- AC-36: Cancel with start_date in past → 422.
- FR-46: Cancellation allowed only before start_date.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| Employee | Cancel own leave (before start) | Cancel after start |
| Manager | Cancel own leave (before start) | Cancel direct reports' leave |

### US-09.3: HR Admin Revokes Leave
As an HR Admin, I want to revoke an approved leave request before it starts so that corrections can be made and the employee's balance is restored.

**Acceptance Criteria:**
- AC-37: Revoke by HR Admin before start_date → 200 + balance restored.
- AC-38: Revoke after start_date → 422.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| HR Admin | Revoke approved leave (before start) | Revoke after start |
| Super Admin | Revoke approved leave (before start) | — |

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-39 | Leave application fields | MUST |
| FR-40 | Draft capability | MUST |
| FR-41 | Full submission validation | MUST |
| FR-42 | Sandwich rule algorithm | MUST |
| FR-43 | Team overlap limit (per department) | MUST |
| FR-44 | Half-day conflict detection | MUST |
| FR-45 | Status lifecycle: Draft → Submitted → PendingL1 → ... → Completed | MUST |
| FR-46 | Employee cancels before start date | MUST |
| FR-47 | HR Admin revokes before start date; balance restored | MUST |
| FR-48 | Retroactive → always requires L2 | MUST |
| FR-49 | Attachment upload constraints | MUST |
| FR-50 | Email notification to manager/HR on submission | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| PT-4 | Leave application with full validation (sandwich, balance display) | Employee |
| PT-5 | Zero balance blocks submit | Employee |
| PT-9 | Cancel leave; revoke after start date blocked | Employee, HR Admin |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| leave_requests | Owner + Manager (team) + HR/SA | Employee, Manager (own) | — (cancel/revoke sets status) |
| approval_steps | Manager (own queue), HR/SA | ApprovalService | — |
| leave_balances | (see F-07) | LeaveBalanceService (deduct on approval, restore on cancel/revoke) | — |
| notifications | — | NotificationService | — |
| audit_logs | — | AuditService | — |

## Integration Points
- F-07 (Leave Balance): balance check on submit; deduct on approval; restore on cancel/revoke
- F-08 (Holiday Calendar): sandwich rule + date validation
- F-10 (Approval Engine): drives approval_steps creation
- F-11 (Notifications): email + in-app on submit/approve/reject/cancel/revoke
- F-12 (Google Calendar): sync on approve/cancel/revoke (via F-11)
- Local filesystem: attachment storage

## HITL Flag
YES — Sandwich rule (FR-42) is the most complex algorithm. Confirm: the rule applies to a single submitted date range only. Non-working days bridging two _separate_ requests are never counted. Implementation should treat each leave request's date range in isolation.

## Execution Wave
Wave 2: Core — requires all Wave 1 foundations (auth, employees, departments, leave types, balances, holidays).

## Dependencies
Depends on: F-01, F-05, F-06, F-07, F-08
Blocks: F-10 (Approval Engine), F-11 (Notifications), F-13 (Dashboards), F-14 (Audit Trail)
