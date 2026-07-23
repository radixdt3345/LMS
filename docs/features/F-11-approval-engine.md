# F-11 — Approval Engine

## Purpose
Enforce the L1/L2 approval chain for leave requests. Routes L1 to the employee's reporting manager or HR Admin (no-manager case). Conditionally requires L2 (HR Admin) for duration > 3 days, RequiresHRFlag leave types, or retroactive requests. When HR Admin acts as L1, L2 is automatically skipped. Manages escalation reminder emails via Hangfire when approvers are inactive for ≥2 days.

## User Stories

### US-11.1: L1 Approval by Manager
As a Manager, I want to approve or reject leave requests from my direct reports (L1) so that my team's availability is managed correctly.

**Acceptance Criteria:**
- AC-44: No-manager employee → pending approval in HR Admin's queue, NOT in any Manager's queue.
- AC-45: HR Admin as L1 approves → leave moves directly to Approved (L2 skipped).
- AC-46: > 3 consecutive days, L1 approved → moves to Pending L2.
- AC-47: Sick Leave (RequiresHRFlag = Yes), L1 approved → moves to Pending L2.
- API-APR-001: Manager approves for non-direct-report → 403.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| Manager | Approve/reject L1 for own direct reports | Approve for non-direct-reports (403) |
| HR Admin | Approve/reject L1 for no-manager employees; all L2 | — |
| Employee | — | Approve any requests |

### US-11.2: L2 Approval by HR Admin
As an HR Admin, I want to handle L2 approvals for leave requests that require additional HR oversight so that policy compliance is enforced.

**Acceptance Criteria:**
- AC-46: L1 approval of >3-day leave triggers Pending L2.
- AC-47: L1 approval of Sick Leave triggers Pending L2.
- AC-39: Retroactive leave → requires_l2 = true, triggers L2 after L1.
- API-APR-002: HR Admin attempts L2 before L1 → 422.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| HR Admin | L2 approve/reject all leave | L2 before L1 complete (422) |
| Super Admin | L2 approve/reject all leave | — |

### US-11.3: Escalation Reminders
As the system, I want to send escalation reminder emails to approvers who have not acted on a pending request within 2 days so that leave requests do not stagnate.

**Acceptance Criteria:**
- AC-48: EscalationJob emails pending approver for all requests with no action for ≥ 2 days.
- FR-60: Daily Hangfire job checks pending L1, L2, and comp-off requests.

**RBAC:** System behavior (Hangfire job).

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-58 | L1 routing (manager or HR Admin); no-manager = skip L2 | MUST |
| FR-59 | L2 conditions: duration > 3d, RequiresHRFlag, retroactive | MUST |
| FR-60 | Daily escalation job; 2-day inactivity reminder | MUST |
| FR-61 | Approval/rejection → immediate email to employee | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| PT-6 | No-manager employee → HR Admin as L1 | Employee, HR Admin |
| PT-7 | Two-level approval for Sick Leave | Employee, Manager, HR Admin |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| approval_steps | Manager (own), HR/SA | ApprovalService | — |
| leave_requests | (see F-09) | ApprovalService (status transitions) | — |
| notifications | — | NotificationService (email + in-app on each approval event) | — |
| audit_logs | — | AuditService | — |

## Integration Points
- F-09 (Leave Requests): drives approval_steps on submission
- F-11 (Notifications): email + in-app on approve/reject; escalation emails
- Hangfire: EscalationJob (daily)
- F-07 (Leave Balance): balance deducted only on final Approved status

## HITL Flag
NO — **RESOLVED (HIL 3):** No-manager rule always wins. When an employee has no reporting manager, HR Admin acts as L1 and L2 is unconditionally skipped — including for retroactive requests. The retroactive flag (requires_l2 = true) only applies when a normal Manager is L1. If HR Admin is L1 (no-manager case), the leave is approved directly after L1 regardless of retroactive status, duration, or RequiresHRFlag.

## Execution Wave
Wave 2: Core — depends on leave requests existing (F-09).

## Dependencies
Depends on: F-09 (Leave Requests), F-05 (employee → manager relationship)
Blocks: F-12 (Notifications), F-13 (Dashboards — pending approval counts)
