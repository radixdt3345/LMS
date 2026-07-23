# Architecture — LMS (Bridge for Ralph-impl & Planning Skills)

## System Boundaries

Single-org, single-server deployment. Monolith ASP.NET Core API (no Docker, no microservices in Phase 1). React SPA served by Nginx. PostgreSQL native install.

**External systems**: Azure AD (SSO), SendGrid (email — plain text/HTML, no dynamic templates), Google Calendar API v3 (company-wide service account — single credential writes to shared company calendar; no per-user OAuth2).

## Domain Map with Integration Layer Definitions

| Domain | Services | Data Owned | INT Scope |
|--------|----------|------------|-----------|
| Auth | AuthService, TokenService, AccountService | users (auth cols), refresh_tokens | JWT middleware in Program.cs, Azure AD MSAL config, Google Calendar per-user OAuth2 consent, CORS, rate-limiting middleware |
| People | EmployeeService, DepartmentService | users (profile cols), departments | Role auto-derivation trigger on employee save/edit, EntityDropdown API responses for dept+manager lists |
| LeaveCore | LeaveTypeService, LeaveBalanceService, LeaveRequestService, ApprovalService, SandwichRuleEngine, HolidayService | leave_types, leave_balances, leave_requests, approval_steps, holidays | Wiring leave approval → NotificationService.SendEmail + CalendarService.CreateEvent (Hangfire enqueue); balance deduction/restoration on approve/cancel/revoke; team overlap DB-level optimistic locking |
| CompOff | CompOffRequestService, CompOffCreditService | comp_off_requests, comp_off_credits | Wiring comp-off approval → increment LeaveBalance via LeaveBalanceService; trigger notification email; comp-off expiry job integration |
| Scheduling | Hangfire job classes | Hangfire schema (PostgreSQL) | Hangfire registration in Program.cs, retry policies (email: 5x/24h; calendar: 3x exp backoff), scheduled job cron expressions (IST-offset UTC) |
| Notifications | NotificationService, EmailService (SendGrid), CalendarService (Google) | notifications | Axios 60s polling for unread count, notification bell badge, SendGrid plain-text/HTML email construction, Google Calendar service account JSON key (env secret) — creates/deletes events on shared company calendar |
| Reporting | ReportService, AuditService | audit_logs (append-only) | AuditService.LogAsync() wired into every domain service state-change; CSV streaming response pipeline |

## Service Responsibilities

| Service | Layer | Key Methods |
|---------|-------|-------------|
| AuthService | Application | LoginAsync, SsoCallbackAsync, LogoutAsync |
| TokenService | Application | IssueAccessToken, IssueRefreshToken, ValidateRefreshToken |
| AccountService | Application | LockAccount, UnlockAccount, GetLockedAccounts |
| EmployeeService | Application | CreateEmployee, UpdateEmployee, DeactivateEmployee, GetTeam, DeriveRole |
| DepartmentService | Application | CreateDepartment, UpdateDepartment, DeactivateDepartment |
| LeaveTypeService | Application | CreateLeaveType, UpdateLeaveType, DeactivateLeaveType |
| LeaveBalanceService | Application | GetBalance, DeductBalance, RestoreBalance, CreditAnnual, ProrateForNewJoiner, YearEndLapse |
| LeaveRequestService | Application | CreateDraft, SubmitRequest, CancelRequest, RevokeRequest, ValidateSubmission |
| SandwichRuleEngine | Domain | ComputeLeaveDays (applies sandwich rule algorithm — FR-42) |
| ApprovalService | Application | ApproveL1, ApproveL2, RejectRequest, GetPendingApprovals |
| HolidayService | Application | CreateHoliday, BulkImport, IsHoliday |
| CompOffRequestService | Application | SubmitRequest, ApproveRequest, RejectRequest |
| CompOffCreditService | Application | CreditBalance, ExpireCredits |
| NotificationService | Application | CreateNotification, MarkRead, GetUnreadCount |
| EmailService | Infrastructure | SendEmailAsync (SendGrid v3, plain text / inline HTML) |
| CalendarService | Infrastructure | CreateLeaveEvent, DeleteLeaveEvent (Google Calendar v3) |
| ReportService | Application | GetUtilization, GetTrends, GetCompliance, ExportCsv |
| AuditService | Application | LogAsync (append-only) |

## API Contract Summary

- Base: `/api/v1/`
- Auth: Bearer JWT in Authorization header
- Error: `{ "success": false, "error": { "code": "...", "message": "...", "details": [] } }`
- Success: `{ "success": true, "data": {...} }` (lists add `total`, `page`, `limit`)
- Status codes: 200/201/204/400/401/403/404/409/422/423/429/500/503
- Pagination: `?page=1&limit=20` (max 100)
- File upload: `multipart/form-data`
- CSV export: `Content-Type: text/csv` streaming response

## Data Ownership Rules

- Each domain owns its tables — no other domain reads them via ORM directly
- Cross-domain reads go through application service interfaces
- `audit_logs`: written by AuditService (called from all domains); read only by Reporting domain
- `leave_balances`: owned by LeaveCore; mutated by CompOff domain only via `LeaveBalanceService` interface call
- `notifications`: written by Notifications domain on behalf of all other domains via `NotificationService`

## Key Architectural Decisions

1. **No Docker** — direct host deployment (IIS / systemd + native PostgreSQL + Nginx)
2. **SendGrid plain text / inline HTML** — no dynamic templates, no template IDs in config
3. **IMemoryCache** for holiday list, leave types, department list (1-hour TTL; invalidated on mutation)
4. **No Redis** in Phase 1
5. **Hangfire PostgreSQL** storage (same DB, `hangfire` schema)
6. **Sandwich rule computed in-process** using cached holiday list (no extra DB round-trip)
7. **IST applied in app logic** — DB stores UTC timestamps; IST conversion at application layer
8. **Google Calendar per-user OAuth2 consent** (not domain-wide delegation)
