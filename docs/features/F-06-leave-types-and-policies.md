# F-06 — Leave Types and Policies

## Purpose
Allow HR Admin and Super Admin to configure leave types (name, code, annual days, attachment requirement, HR flag, active status). Leave types drive all leave balance allocation, approval routing, and sandwich rule behavior. Seeded defaults cover the 5 standard types.

## User Stories

### US-06.1: Manage Leave Types
As an HR Admin, I want to create and manage leave types with configurable fields so that leave policies are enforced consistently across the system.

**Acceptance Criteria:**
- AC-20: POST /api/v1/leave-types by HR Admin with valid payload → HTTP 201.
- FR-27: Fields: Name, Code, Description, AnnualLeaveDays, RequiresAttachment, RequiresHRFlag, IsActive.
- FR-29: Entitlement comes directly from leave type — no per-employee override.
- FR-30: Leave year = Jan 1 – Dec 31; no carry-forward.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| HR Admin | Full CRUD on leave types | — |
| Super Admin | Full CRUD on leave types | — |
| Manager / Employee | Read leave types (for dropdown and balance display) | Create/Edit/Delete |

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-27 | Configurable leave type fields | MUST |
| FR-28 | Seed 5 default leave types | MUST |
| FR-29 | Entitlement from leave type only | MUST |
| FR-30 | Leave year Jan 1–Dec 31, no carry-forward | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| — | Leave type CRUD verified via API tests | HR Admin |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| leave_types | All roles | HR Admin, Super Admin | HR Admin, Super Admin (soft only) |
| audit_logs | — | AuditService | — |

## Integration Points
- Leave Balance (F-07): AnnualLeaveDays drives proration and new-year credit jobs.
- Leave Requests (F-08): RequiresAttachment and RequiresHRFlag drive validation and L2 routing.

## HITL Flag
NO

## Execution Wave
Wave 1: Foundation — leave types must exist before leave balances can be allocated.

## Dependencies
Depends on: F-03 (seed creates default leave types)
Blocks: F-07 (Leave Balance Management), F-08 (Leave Requests)
