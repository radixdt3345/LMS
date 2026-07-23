# F-05 — Employee Management

## Purpose
Allow HR Admin and Super Admin to create, edit, and soft-delete employee profiles with validated dropdown links to departments and reporting managers. Implements auto-derivation of the Manager role from reporting structure, and enforces the demotion block when a manager still has direct reports.

## User Stories

### US-05.1: HR Admin Creates and Manages Employees
As an HR Admin, I want to create and manage employee profiles with linked departments and reporting managers so that the system's approval routing and RBAC are correct.

**Acceptance Criteria:**
- AC-11: POST /api/v1/employees by HR Admin with valid payload → HTTP 201.
- AC-12: POST /api/v1/employees by Manager → HTTP 403.
- AC-13: DELETE sets status = Inactive; record retained.
- AC-14: Saving employee with reporting_manager_id = User X (Employee role) → User X promoted to Manager.
- AC-15: Removing last direct report of User X → User X demoted to Employee.
- AC-16: Changing Manager to Employee while active direct reports exist → HTTP 422 with demotion block message.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| HR Admin | Full CRUD on employees | — |
| Super Admin | Full CRUD on employees | — |
| Manager | Read own team (GET /employees/team) | Create/Edit/Delete other employees |
| Employee | Read/edit own name+phone only | Access other employee data |

### US-05.2: Employee Views and Edits Own Profile
As an Employee, I want to view and update my own name and phone number so that my contact information stays current.

**Acceptance Criteria:**
- FR-19: Employee can edit name and phone only (not email, role, department, reporting manager).
- AC-17: GET /employees/team by Manager with no direct reports → 403 or empty (no team menu shown).
- FR-20: Manager/Employee sees subordinate list only if at least one employee reports to them.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| Employee | Read/edit own name, phone | Edit role, department, email, reporting manager |
| Manager | All of Employee + read own team | Edit other employees |

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-12 | Employee profile fields | MUST |
| FR-13 | Dropdown-linked entities only (no free text) | MUST |
| FR-14 | No manager → routes to HR Admin | MUST |
| FR-15 | Only HR Admin/Super Admin can CRUD employees | MUST |
| FR-16 | Soft delete | MUST |
| FR-17 | Auto-promote/demote Manager role from reporting structure | MUST |
| FR-18 | Block demotion if manager has active direct reports | MUST |
| FR-19 | Employee can edit own name and phone only | MUST |
| FR-20 | Subordinate list/menu visible only if direct reports exist | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| PT-3 | HR Admin creates employee with reporting manager; manager role auto-promoted | HR Admin |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| users (profile) | All (scoped by role) | HR Admin, Super Admin, Employee (own name/phone) | HR Admin, Super Admin (soft only) |
| audit_logs | — | AuditService | — |

## Integration Points
- Leave balance creation on employee onboarding (triggers proration — F-08)

## HITL Flag
YES — Role auto-derivation (FR-17 / FR-18) has edge cases: What happens if HR Admin creates two employees simultaneously, both pointing to the same manager (User X)? The first save promotes X; the second save should idempotently see X is already Manager. Confirm: role promotion is idempotent (setting Manager on an already-Manager user is a no-op).

## Execution Wave
Wave 1: Foundation — departments (F-04) must exist first; employee records drive all downstream features.

## Dependencies
Depends on: F-01 (auth), F-03 (seed), F-04 (departments)
Blocks: F-06 (Leave Types), F-07 (Leave Balance), F-08 (Leave Requests), F-09 (Comp-off), F-10 (Approval Engine)
