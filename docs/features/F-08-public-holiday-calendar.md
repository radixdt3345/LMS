# F-08 — Public Holiday Calendar

## Purpose
Allow HR Admin to create, edit, delete, and bulk-import public holidays. The holiday list is consumed by the sandwich rule engine, leave validation (no leave on holidays), comp-off eligibility, and the leave application calendar UI (holiday dates disabled).

## User Stories

### US-08.1: Manage Public Holidays
As an HR Admin, I want to add, edit, and delete public holidays (individually and via CSV bulk import) so that all leave validations and the employee calendar reflect correct working days.

**Acceptance Criteria:**
- AC-49: POST /api/v1/holidays by HR Admin with valid date and name → HTTP 201.
- AC-50: POST /api/v1/holidays/bulk-import with valid CSV → HTTP 200 with count imported.
- AC-51: In Apply for Leave UI, holiday dates are disabled and cannot be selected.
- FR-64: Holidays factored into sandwich rule, working-day counting, team overlap, comp-off eligibility.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| HR Admin | Full CRUD + bulk CSV import | — |
| Super Admin | Full CRUD + bulk CSV import | — |
| Employee / Manager | Read holidays (calendar view, dropdown) | Create/Edit/Delete |

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-62 | HR Admin manages holidays (date, name) | MUST |
| FR-63 | Bulk import via CSV | MUST |
| FR-64 | Holidays used in validation logic | MUST |
| FR-65 | Calendar UI disables holidays as start/end dates | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| PT-10 | HR Admin manages holidays; calendar disables them in apply form | HR Admin, Employee |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| holidays | All roles | HR Admin, Super Admin | HR Admin, Super Admin |
| audit_logs | — | AuditService | — |

## Integration Points
- F-09 (Leave Requests): sandwich rule and date validation consume holiday list
- F-10 (Comp-off Requests): comp-off date must be holiday or weekend
- Frontend leave application calendar

## HITL Flag
NO

## Execution Wave
Wave 1: Foundation — holiday data must exist before any leave validation can run correctly.

## Dependencies
Depends on: F-01 (auth), F-05 (employees — to know who has CRUD access)
Blocks: F-09 (Leave Requests — date/sandwich validation), F-10 (Comp-off — date eligibility)
