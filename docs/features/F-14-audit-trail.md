# F-14 — Audit Trail

## Purpose
Maintain an append-only, tamper-proof audit log recording every state-changing action in the system — including who, what changed (old/new JSON), when, and from which IP. HR Admin and Super Admin can search and filter the audit log. Log retained for minimum 3 years.

## User Stories

### US-14.1: Audit Log Search and View
As an HR Admin or Super Admin, I want to search and view the complete audit trail filtered by user, action type, date range, and entity type so that I can fulfill compliance and investigation obligations.

**Acceptance Criteria:**
- AC-61: Every leave approval stores AuditLog row with action = LEAVE_APPROVED, old_value, new_value, user_id, ip_address.
- AC-62: DELETE on audit_log → 405 (or blocked at application layer).
- AC-63: GET /api/v1/audit-log?user_id=X&action=LEAVE_APPROVED&date_from=Y&date_to=Z → 200 with filtered results.
- FR-78: Auditable actions include all state-changing events (leave CRUD, approvals, cancellations, revocations, comp-off, leave type/policy changes, holiday changes, role changes, employee changes, department CRUD, account lock/unlock, login/logout).
- FR-81: Retained for minimum 3 years.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| HR Admin | Search and view full audit log | Delete/edit audit log |
| Super Admin | Search and view full audit log | Delete/edit audit log |
| Employee / Manager | — | Access audit log |

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-77 | Log: who, what (old→new JSON), when, IP | MUST |
| FR-78 | All auditable action types | MUST |
| FR-79 | Append-only (no edit/delete) | MUST |
| FR-80 | Searchable by user, action, date range, entity type | MUST |
| FR-81 | 3-year retention | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| PT-14 | HR Admin searches audit log; leave approval event visible | HR Admin |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| audit_logs | HR Admin, Super Admin | AuditService (all state-changing events) | BLOCKED — 405 |

## Integration Points
- Called by every domain service on state change: AuthService (login/logout), EmployeeService (profile changes), DepartmentService, LeaveTypeService, LeaveRequestService, CompOffRequestService, ApprovalService, AccountService, HolidayService

## HITL Flag
NO

## Execution Wave
Wave 2: Core — AuditService is called by all other domain services; must be built in Wave 2 to be available to Wave 2 features.

## Dependencies
Depends on: F-01 (auth — user_id on every log entry)
Blocks: NONE (cross-cutting concern referenced by all features)
