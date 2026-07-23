# F-04 — Department Management

## Purpose
Allow HR Admin and Super Admin to create, edit, deactivate, and view departments. Departments are a flat list with unique names/codes, team overlap limits, and soft-delete semantics that preserve historical leave data.

## User Stories

### US-04.1: CRUD for Departments
As an HR Admin, I want to create, view, edit, and soft-delete departments so that the organizational structure is maintained accurately.

**Acceptance Criteria:**
- AC-18: Duplicate department name (case-insensitive) returns HTTP 422.
- AC-19: DELETE on department with active employees returns HTTP 422.
- FR-23: Department name and code unique among active departments.
- FR-24: Soft delete only; department with active employees cannot be deleted.
- FR-25: Deactivating does not auto-reassign employees.
- FR-26: All CRUD actions recorded in Audit Trail.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| HR Admin | Full CRUD on departments | — |
| Super Admin | Full CRUD on departments | — |
| Manager | Read departments (dropdown) | Create/Edit/Delete |
| Employee | Read departments (dropdown) | Create/Edit/Delete |

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-21 | Department fields: name, code, team overlap limit, status | MUST |
| FR-22 | Flat department list, no hierarchy | MUST |
| FR-23 | Name and code unique (case-insensitive) | MUST |
| FR-24 | Soft delete; blocks delete if active employees | MUST |
| FR-25 | Deactivation doesn't reassign employees | MUST |
| FR-26 | Audit trail for all department CRUD | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| PT-10 | HR Admin creates department, edits it, soft-deletes | HR Admin |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| departments | All roles | HR Admin, Super Admin | HR Admin, Super Admin (soft only) |
| audit_logs | — | AuditService | — |

## Integration Points
None

## HITL Flag
NO

## Execution Wave
Wave 1: Foundation — departments must exist before employees (FK dependency).

## Dependencies
Depends on: F-03 (seed creates default department)
Blocks: F-05 (Employee Management requires departments)
