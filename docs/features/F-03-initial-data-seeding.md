# F-03 — Initial Data Seeding

## Purpose
On first deployment, idempotently seed the database with the minimum viable data: one Super Admin user, one HR Admin user, one default "HR" department, and 5 default leave types (Casual, Sick, Earned, Comp-off, Unpaid). Re-running the seed must not create duplicates.

## User Stories

### US-03.1: System Seed on Deployment
As a Super Admin, I want the system to be pre-seeded with default users, a department, and leave types on first deployment so that the system is operational immediately without manual configuration.

**Acceptance Criteria:**
- AC-64: Seed on empty DB creates exactly 1 Super Admin user, 1 HR Admin user, 1 HR department, and 5 leave types.
- AC-65: Running seed script twice results in same 1+1+1+5 records — no duplicates.
- FR-83: Both seeded accounts use password Admin@123.
- FR-28: 5 leave types seeded: Casual (12d), Sick (6d, RequiresAttachment=Yes, RequiresHRFlag=Yes), Earned (1d), Comp-off (0d), Unpaid (0d).

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| System (deployment) | Run seed script | — |
| Super Admin | Re-run seed idempotently | — |

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-28 | Seed 5 default leave types | MUST |
| FR-82 | Seed Super Admin, HR Admin, HR department, 5 leave types | MUST |
| FR-83 | Seeded accounts use Admin@123 | MUST |
| FR-84 | Seed script is idempotent | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| — | (Seeding verified via IT, not E2E) | — |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| users | — | DataSeeder | — |
| departments | — | DataSeeder | — |
| leave_types | — | DataSeeder | — |

## Integration Points
None (purely database)

## HITL Flag
NO

## Execution Wave
Wave 1: Foundation — must run before any functional testing. Blocks all other features from working in an empty DB.

## Dependencies
Depends on: NONE (but runs after schema migration)
Blocks: F-01 (needs users to exist), F-04, F-05, F-06, F-07
