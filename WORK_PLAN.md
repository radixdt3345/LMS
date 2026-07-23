# Work Plan — Leave Management System (LMS)

## Epics (Phase 1)

| # | Epic | Domain | Wave |
|---|------|--------|------|
| E1 | Authentication & Account Security | Auth | Wave 1 |
| E2 | Employee & Department Management | People | Wave 1 |
| E3 | Leave Types, Policies & Balances | LeaveCore | Wave 1 |
| E4 | Leave Request & Approval Workflow | LeaveCore | Wave 2 |
| E5 | Comp-Off Request & Credits | CompOff | Wave 2 |
| E6 | Public Holiday Calendar | LeaveCore | Wave 1 |
| E7 | Background Jobs & Scheduling | Scheduling | Wave 2 |
| E8 | Notifications (Email + In-App + Calendar) | Notifications | Wave 2 |
| E9 | Dashboards & Reporting | Reporting | Wave 3 |
| E10 | Audit Trail | Reporting | Wave 2 |
| E11 | Initial Data Seeding | People/Auth | Wave 1 |

## Execution Waves

### Wave 1 — Foundation (must complete before Wave 2)
- Database schema + EF Core migrations
- Authentication (JWT, Azure AD SSO, local login, refresh, logout, account lock)
- Employee CRUD (with role auto-derivation)
- Department CRUD
- Leave types + initial balance credit
- Public holiday calendar CRUD
- Initial data seeding (Super Admin, HR Admin, default dept, default leave types)

### Wave 2 — Core Features (must complete before Wave 3)
- Leave request application, validation (sandwich rule, balance check, overlap, team limit)
- Approval engine (L1/L2, no-manager routing, retroactive handling)
- Leave cancel + revoke
- Comp-off request submit + approve + credit + expiry
- Background jobs (escalation, comp-off expiry, year-end lapse, new-year credit)
- Email notifications (SendGrid)
- In-app notification center
- Google Calendar sync (Hangfire)
- Audit trail (all auditable actions)

### Wave 3 — Reporting & Dashboards
- Employee dashboard
- Manager dashboard (team calendar, pending approvals, team balance)
- HR dashboard (utilization chart, trend chart, compliance report, CSV export)
- Super Admin dashboard (system-wide metrics)
- Full audit log viewer (search, filter, pagination)
- Department reports + CSV export

## Hard External Dependencies

| Dependency | Type | Risk |
|-----------|------|------|
| Azure AD tenant + app registration | Credential | Required before SSO can be tested |
| SendGrid API key + dynamic templates | Credential + Setup | Required before email notifications can be tested end-to-end |
| Google Calendar OAuth2 credentials | Credential | Required before calendar sync can be tested |
| PostgreSQL 15+ instance | Infrastructure | Provided via Docker Compose for local dev |

## Completion Signal
All tasks in `task_status.md` = COMPLETE + `/production-readiness` check passed.
