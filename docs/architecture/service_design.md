# Service Design — Leave Management System (LMS)

## Service Inventory

All services run within a single ASP.NET Core Web API process. They are separated by namespace and interface boundaries, not separate deployable services (Phase 1 is a monolith-first approach; service extraction is a Phase 2 concern).

| Domain | Service(s) | Responsibility | Data Owned |
|--------|-----------|----------------|------------|
| Auth | `AuthService`, `TokenService`, `AccountService` | Login (SSO + local), JWT issuance, refresh, logout, account lock/unlock | `users` (auth fields: password_hash, failed_login_attempts, locked_at), `refresh_tokens` |
| People | `EmployeeService`, `DepartmentService` | Employee CRUD, role auto-derivation, department CRUD, reporting structure | `users` (profile fields), `departments` |
| LeaveCore | `LeaveTypeService`, `LeaveBalanceService`, `LeaveRequestService`, `HolidayService` | Leave type CRUD, balance management (credit, deduct, prorate, year-end lapse), leave request lifecycle (apply, validate, submit, cancel, revoke), holiday CRUD + CSV import, sandwich rule engine | `leave_types`, `leave_balances`, `leave_requests`, `approval_steps`, `holidays` |
| CompOff | `CompOffRequestService`, `CompOffCreditService` | Comp-off request submit/approve/reject, credit lifecycle, expiry tracking | `comp_off_requests`, `comp_off_credits` |
| Scheduling | `EscalationJob`, `CompOffExpiryJob`, `YearEndLapseJob`, `NewYearCreditJob`, `CalendarSyncJob`, `CalendarDeleteJob`, `EmailDispatchJob` | All background Hangfire jobs; delegate to domain services | No owned tables — mutates data via domain services |
| Notifications | `NotificationService`, `EmailService`, `CalendarService` | In-app notification CRUD, SendGrid email dispatch, Google Calendar event create/delete | `notifications` |
| Reporting | `ReportService`, `AuditService` | Dashboard data aggregation, CSV export, audit log writes and search | `audit_logs` (append-only) |

## Inter-Service Communication

| Pattern | Used For |
|---------|---------|
| **In-process method call** (synchronous) | All request-response flows — controllers call application services |
| **Hangfire enqueue** (async fire-and-forget) | Email dispatch, Google Calendar sync, triggered within synchronous workflows |
| **Hangfire recurring job** | Escalation (daily), comp-off expiry (daily), year-end lapse (Dec 31), new-year credit (Jan 1) |
| **No external message bus** | Not required for Phase 1 scale (500 concurrent users) |

## Domain Map

### Domain 1 — Auth
**Description**: Handles all authentication concerns — SSO, local login, token lifecycle, account locking.
**Services**: `AuthService`, `TokenService`, `AccountService`
**Data Owned**: `users` (auth columns), `refresh_tokens`
**Integration Layer Definition**: INT issues for Auth cover: JWT middleware registration in `Program.cs`, Azure AD MSAL configuration, Google Calendar per-user OAuth2 consent flow integration with the Notifications domain. Also covers CORS configuration and rate-limiting middleware.

### Domain 2 — People
**Description**: Employee directory and department management, including role auto-derivation from reporting structure.
**Services**: `EmployeeService`, `DepartmentService`
**Data Owned**: `users` (profile columns: name, email, phone, department_id, designation, date_of_joining, reporting_manager_id, role, status), `departments`
**Integration Layer Definition**: INT issues for People cover: the role auto-derivation trigger (Employee → Manager promotion/demotion logic that fires on employee create/edit). Also covers the `EntityDropdown` API responses that supply active department and manager lists to the frontend.

### Domain 3 — LeaveCore
**Description**: The heart of the system — leave type configuration, balance tracking, the full leave request lifecycle, the sandwich rule engine, the approval engine (L1/L2), holiday calendar, and all leave validation rules.
**Services**: `LeaveTypeService`, `LeaveBalanceService`, `LeaveRequestService`, `ApprovalService`, `SandwichRuleEngine`, `HolidayService`
**Data Owned**: `leave_types`, `leave_balances`, `leave_requests`, `approval_steps`, `holidays`
**Integration Layer Definition**: INT issues for LeaveCore cover: wiring leave approval to trigger `NotificationService.SendLeaveApprovedEmailAsync()` and `CalendarService.CreateLeaveEventAsync()` (via Hangfire enqueue). Also covers the balance deduction/restoration callbacks on approval, cancel, and revoke. Team overlap DB-level locking (first-commit-wins) implementation.

### Domain 4 — CompOff
**Description**: Comp-off request submission and approval, credit lifecycle, and expiry. Closely tied to LeaveCore (credits comp-off balance in `leave_balances`).
**Services**: `CompOffRequestService`, `CompOffCreditService`
**Data Owned**: `comp_off_requests`, `comp_off_credits`
**Integration Layer Definition**: INT issues for CompOff cover: wiring comp-off approval to increment `leave_balances.balance` via `LeaveBalanceService` (cross-domain service call), and to trigger the notification email to the employee. Also covers the comp-off expiry job integration with `CompOffCreditService` and `LeaveBalanceService`.

### Domain 5 — Scheduling
**Description**: All Hangfire background jobs. Jobs are thin orchestrators — they call domain services, never repositories.
**Services**: All Hangfire job classes in `LMS.Infrastructure.Jobs`
**Data Owned**: None (Hangfire persistence is in `hangfire` schema in PostgreSQL)
**Integration Layer Definition**: INT issues for Scheduling cover: Hangfire registration in `Program.cs` (PostgreSQL storage, recurring job schedules, dashboard config). Also covers retry policies for `EmailDispatchJob` (5x, 24h) and `CalendarSyncJob` (3x, exponential backoff), and ensuring job failure does not cascade to leave operation rollback.

### Domain 6 — Notifications
**Description**: In-app notification persistence, SendGrid email delivery, and Google Calendar event sync.
**Services**: `NotificationService`, `EmailService` (SendGrid wrapper), `CalendarService` (Google Calendar wrapper)
**Data Owned**: `notifications`
**Integration Layer Definition**: INT issues for Notifications cover: Axios polling integration (60-second poll for unread count), the notification bell badge in the frontend navbar, and the per-user Google Calendar OAuth2 consent flow (redirect + callback + token storage). Also covers SendGrid dynamic template mapping.

### Domain 7 — Reporting
**Description**: Dashboard data queries, CSV generation, and the append-only audit trail.
**Services**: `ReportService`, `AuditService`
**Data Owned**: `audit_logs`
**Integration Layer Definition**: INT issues for Reporting cover: `AuditService.LogAsync()` wiring into every domain service that performs a state-changing action (cross-cutting concern). The audit interceptor or decorator pattern. Also covers the CSV download response pipeline (streaming response, Content-Type: text/csv).
