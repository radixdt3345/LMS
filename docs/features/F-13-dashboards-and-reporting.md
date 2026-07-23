# F-13 — Dashboards and Reporting

## Purpose
Provide role-appropriate dashboards for Employee, Manager, HR Admin, and Super Admin — each with relevant leave data, charts, and quick actions. All reports support date range filtering and CSV export.

## User Stories

### US-13.1: Employee Dashboard
As an Employee, I want to see my leave balance cards, recent leave history, quick action buttons, and upcoming holidays so that I have all relevant information at a glance.

**Acceptance Criteria:**
- FR-72: Balance cards per active leave type (used/total with progress bar), Apply for Leave + Request Comp-Off quick actions, last 5 leave requests table, next 30 days' public holidays.
- AC-57: Balance cards update after leave changes.

**RBAC:** Employee, Manager (also sees Employee Dashboard).

### US-13.2: Manager Dashboard
As a Manager, I want to see pending approvals, a team calendar, and team balance summary so that I can manage my team's availability.

**Acceptance Criteria:**
- FR-73: Pending approvals count card (leave + comp-off) with link; team calendar (month view, direct reports); team balance summary table.
- AC-58: Manager with no subordinates → no Manager Dashboard or Subordinate List in nav.

**RBAC:** Manager (visible only when at least one direct report exists).

### US-13.3: HR Admin Dashboard
As an HR Admin, I want to see department-wise utilization charts, trend lines, and compliance alerts so that I can monitor leave health organization-wide.

**Acceptance Criteria:**
- FR-74: Department-wise utilization bar chart, monthly leave trend line chart, policy compliance alert list, top cards (Total Leaves Today, Pending L2 Approvals, Policy Violations This Month).
- AC-59: GET /api/v1/reports/utilization → 200 with per-department data.

**RBAC:** HR Admin, Super Admin.

### US-13.4: Super Admin Dashboard
As a Super Admin, I want to see system-wide metrics (total employees, total leaves today, pending approvals, policy violations) and full audit log access.

**Acceptance Criteria:**
- FR-75: Total Active Employees, Total Leaves Today (system-wide), Pending Approvals (all levels), Policy Violations This Month.

**RBAC:** Super Admin only.

### US-13.5: Reports and CSV Export
As an HR Admin, I want to generate leave utilization, trend, and compliance reports with date range filtering and export them as CSV so that I can fulfill reporting obligations.

**Acceptance Criteria:**
- FR-76: All reports support date range filtering.
- AC-60: GET /api/v1/reports/export?date_from=...&date_to=... → 200 with Content-Type: text/csv.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| HR Admin / Super Admin | All reports + CSV export | — |
| Manager | Team-level reports only | — |
| Employee | Own leave history | — |

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-72 | Employee Dashboard | MUST |
| FR-73 | Manager Dashboard | MUST |
| FR-74 | HR Admin Dashboard | MUST |
| FR-75 | Super Admin Dashboard | MUST |
| FR-76 | Date range filtering + CSV export | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| PT-12 | Employee Dashboard loads with correct balance cards | Employee |
| PT-13 | HR Admin Dashboard loads with utilization chart | HR Admin |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| leave_requests | Scoped by role | — | — |
| leave_balances | Scoped by role | — | — |
| comp_off_requests | Scoped by role | — | — |
| notifications | Own | — | — |
| holidays | All | — | — |

## Integration Points
- F-07 (Leave Balance): balance card data
- F-09 (Leave Requests): recent requests, team calendar, approval queue
- F-10 (Comp-off): pending comp-off count
- F-11 (Approval Engine): pending approval counts
- Chart.js / react-chartjs-2: bar chart, line chart
- FullCalendar: team calendar month view

## HITL Flag
NO

## Execution Wave
Wave 3: Enhancement — requires all Wave 2 features (leave requests, comp-off, approvals) to have data to display.

## Dependencies
Depends on: F-07, F-09, F-10, F-11, F-12
Blocks: NONE
