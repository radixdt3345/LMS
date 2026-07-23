# Agent Routing — Leave Management System (LMS)

## Domain Map

| Domain | Bounded Context | Primary Layers |
|--------|----------------|----------------|
| Auth | Authentication, JWT, SSO, account locking, refresh tokens | DB, API |
| People | Employees, departments, role auto-derivation, RBAC enforcement | DB, API, UI |
| LeaveCore | Leave types, balances, leave requests, approval engine, holiday calendar | DB, API, UI |
| CompOff | Comp-off requests, comp-off credits, comp-off expiry | DB, API, UI |
| Scheduling | Hangfire jobs (escalation, expiry, year-end lapse, new-year credit, calendar sync) | API (job layer) |
| Notifications | In-app notifications, email (SendGrid), Google Calendar sync | DB, API, UI |
| Reporting | Dashboards, charts, reports, CSV export, audit trail | DB, API, UI |

## Routing Rules

- **DB, API, UI, INT issues** → Ralph-impl
- **TEST issues** → Ralph-test (runs after all sibling impl issues close in that domain/stage)
- **E2E issues** → Ralph-e2e (runs after TEST issues close for the critical path)

## Concurrency Caps

- Max domain agents (Tier 1): 4
- Max issue workers per stage within a domain (Tier 2): 3

## Domain Dependency Order

```
Wave 1 (parallel):
  Auth DB → Auth API
  People DB → People API → People UI
  LeaveCore (types/holidays) DB → LeaveCore API → LeaveCore UI (partial)
  Seeding (after Auth + People + LeaveCore DB)

Wave 2 (after Wave 1 complete):
  LeaveCore (requests/approvals) API → UI
  CompOff DB → CompOff API → CompOff UI
  Scheduling API (Hangfire jobs)
  Notifications DB → Notifications API → Notifications UI
  Reporting (Audit) DB → Reporting API

Wave 3 (after Wave 2 complete):
  Reporting (Dashboards/Reports) API → UI
```

## PR Target Branch
- `main` (single-branch strategy)

## Agent Models
- All agents: `claude-sonnet-4-6`
