# Product Requirements Document — Leave Management System (LMS)

**Version**: 1.0 (derived from LMS_REQUIREMENTS_v1.3.md)
**Date**: July 2026
**Status**: Pending HIL 1 Review

---

## Section 1 — Executive Summary

**Product Name**: Leave Management System (LMS)

The Leave Management System is an enterprise web application that replaces manual leave tracking (spreadsheets, emails, verbal approvals) with an automated, policy-driven system handling the full leave lifecycle — from employee application through multi-level approval, balance tracking, and compliance reporting — for a single-organization deployment.

**Problem**: Manual leave tracking via spreadsheets and emails creates approval bottlenecks, inconsistent policy enforcement, zero real-time visibility, and non-auditable records that expose the organization to compliance and operational risk.

**Target Users**: Employee, Manager, HR Admin, Super Admin (see Section 3).

**Success Metrics**:
- Zero manual leave tracking processes post go-live
- All 22 screens operational with correct RBAC enforcement
- Sandwich rule, balance validation, team overlap enforced automatically on every submission
- Audit trail capturing all state-changing actions, retained for 3+ years
- p99 API response < 500ms (CRUD), p99 < 2s (reports)
- 99.9% system uptime

---

## Section 2 — Scope

### In Scope (Phase 1)
- Authentication: Azure AD SSO (OAuth2) + local email/password fallback
- Employee and department management (CRUD, soft delete, role auto-derivation)
- Leave types and policies (configurable; 5 default types)
- Leave balance management (annual credit, proration, real-time update, year-end lapse)
- Leave request workflow (apply, draft, submit, L1/L2 approval, cancel, revoke)
- Comp-off request workflow (submit, approve, credit, 30-day expiry)
- Approval engine (L1/L2, no-manager routing, retroactive handling, sandwich rule)
- Public holiday calendar (CRUD, bulk CSV import)
- Email notifications via SendGrid
- In-app notification center
- Google Calendar sync on leave approval/cancel/revoke
- Background jobs via Hangfire (escalation, expiry, year-end lapse, calendar sync)
- Dashboards per role (Employee, Manager, HR Admin, Super Admin)
- Reporting (utilization, trends, compliance, CSV export)
- Audit trail (append-only, 3-year retention)
- Initial data seeding (2 default users, 1 department, 5 leave types)
- 22 screens with role-based visibility

### Out of Scope (Phase 1)
- Mobile application
- Payroll integration and salary deductions
- Time tracking and attendance
- Leave encashment processing
- Multi-language / internationalization
- Multi-currency, multi-timezone (IST only)
- Outlook calendar integration
- PDF report export (CSV only)
- S3 file storage (local filesystem only)
- Past data migration
- Carry-forward of leave balances
- Negative-balance leave
- Leave application for HR Admin / Super Admin roles
- Configurable comp-off expiry (fixed at 30 days)
- Leave accrual schedules (monthly/quarterly) — annual lump-sum only
- Optional/regional holidays
- Bulk approve/reject
- Approval delegation / auto-escalation to next level
- Admin UI for system configuration (Azure AD role mapping via app config only)

### Future Phases
Phase 2 targets: S3 attachment storage, multi-timezone, Outlook calendar integration, PDF export, payroll integration, mobile application.

---

## Section 3 — User Roles

| Role | Description | Primary Goals |
|------|-------------|---------------|
| Employee | Any staff member | Apply for leave, view own balances/history, request comp-off |
| Manager | Team lead with direct reports | Approve/reject L1 leave and comp-off, monitor team availability and balances |
| HR Admin | HR team member | Manage employees/departments/leave types/holidays, L2 approvals, revocation, reporting, audit |
| Super Admin | System administrator | Full access — all data, system-wide metrics, audit log, locked account management |
| Unauthenticated | Visitor not yet logged in | Reach login screen; cannot access any other route |

---

## Section 4 — Functional Requirements

### Domain: Authentication & Security

```
FR-1: The system shall support Azure AD SSO via OAuth2 Authorization Code Flow.
Priority: MUST
Roles: All

FR-2: Azure AD group membership shall map to LMS roles via application configuration (no admin UI).
Priority: MUST
Roles: All

FR-3: The system shall support local email + password login as a fallback.
Priority: MUST
Roles: All

FR-4: After SSO or local login, the system shall issue a JWT access token (24h expiry) and a refresh token (7d expiry, stored in DB).
Priority: MUST
Roles: All

FR-5: Refresh tokens shall be stored in the database and invalidated on logout.
Priority: MUST
Roles: All

FR-6: Local login passwords shall enforce: minimum 8 characters, at least 1 uppercase letter, at least 1 number.
Priority: MUST
Roles: All

FR-7: Accounts shall lock after 3 consecutive failed local login attempts.
Priority: MUST
Roles: All

FR-8: Locked accounts shall be unlockable by HR Admin or Super Admin via the Locked Account Management screen.
Priority: MUST
Roles: HR Admin, Super Admin

FR-9: JWTs shall contain: user_id, role, department_id.
Priority: MUST
Roles: All

FR-10: Role changes shall take effect on the next token refresh, not mid-session.
Priority: MUST
Roles: All

FR-11: If a new SSO user's AD group does not match any configured mapping, the system shall assign the Employee role by default and create the account as active.
Priority: MUST
Roles: All (system behaviour)
```

### Domain: Employee Management

```
FR-12: Employee profiles shall contain: name, email, phone (nullable), department, designation, date of joining, reporting manager (optional), status (active/inactive).
Priority: MUST
Roles: HR Admin, Super Admin (CRUD); Manager (read own team); Employee (read/edit own name+phone)

FR-13: Employees shall be linked to entities (department, reporting manager, leave type) via dropdowns of active records — no free-text entry.
Priority: MUST
Roles: HR Admin, Super Admin

FR-14: Reporting manager is optional; if unlinked, the employee's leave and comp-off notifications/approvals route to HR Admin.
Priority: MUST
Roles: System behaviour

FR-15: Only HR Admin and Super Admin can create, edit, or soft-delete employees.
Priority: MUST
Roles: HR Admin, Super Admin

FR-16: Employee records shall support soft delete (set inactive; retain data for audit).
Priority: MUST
Roles: HR Admin, Super Admin

FR-17: The Manager role shall be auto-derived from reporting structure: (a) When an employee is saved with reporting_manager_id = User X and User X's role is Employee, set User X's role to Manager. (b) When the last direct report of User X is removed or reassigned, downgrade User X's role to Employee.
Priority: MUST
Roles: System behaviour

FR-18: If an attempt is made to change a Manager's role to Employee while that Manager still has direct reports, the system shall block the change with error: "This user is a reporting manager for active employees and cannot be demoted to Employee."
Priority: MUST
Roles: System behaviour

FR-19: An employee can view and edit their own profile (name, phone only — not email, role, department, or reporting manager).
Priority: MUST
Roles: Employee, Manager

FR-20: A Manager or Employee shall see the subordinate list/menu only if at least one employee reports to them.
Priority: MUST
Roles: Manager, Employee
```

### Domain: Department Management

```
FR-21: Department profiles shall contain: name, code, team overlap limit, status (active/inactive).
Priority: MUST
Roles: HR Admin, Super Admin (CRUD); Manager, Employee (read-only)

FR-22: Departments shall be a flat list with no hierarchy.
Priority: MUST
Roles: System behaviour

FR-23: Department name and code shall be unique (case-insensitive) among active departments.
Priority: MUST
Roles: System behaviour

FR-24: Departments shall support soft delete; a department with active employees cannot be hard-deleted.
Priority: MUST
Roles: HR Admin, Super Admin

FR-25: Deactivating a department shall not reassign its employees; HR Admin/Super Admin must reassign manually.
Priority: MUST
Roles: System behaviour

FR-26: All department CRUD and reactivate actions shall be recorded in the Audit Trail.
Priority: MUST
Roles: System behaviour
```

### Domain: Leave Types & Policies

```
FR-27: The system shall support configurable leave types with fields: Name, Code, Description, AnnualLeaveDays, RequiresAttachment, RequiresHRFlag, IsActive.
Priority: MUST
Roles: HR Admin, Super Admin (CRUD); All (read)

FR-28: On initial deployment, the system shall seed 5 default leave types: Casual Leave (12d), Sick Leave (6d, RequiresAttachment=Yes, RequiresHRFlag=Yes), Earned Leave (1d), Comp-off (0d, credit only), Unpaid Leave (0d, no balance tracking).
Priority: MUST
Roles: System behaviour

FR-29: Every employee shall receive leave entitlements directly from the Leave Type's AnnualLeaveDays — no per-employee, per-department, or per-grade override.
Priority: MUST
Roles: System behaviour

FR-30: The leave year shall be January 1 to December 31, with no carry-forward.
Priority: MUST
Roles: System behaviour
```

### Domain: Leave Balance Management

```
FR-31: The system shall maintain per-employee, per-leave-type balance records.
Priority: MUST
Roles: System behaviour

FR-32: Full-day leave deducts 1.0 day from balance; half-day leave deducts 0.5 days.
Priority: MUST
Roles: System behaviour

FR-33: Annual entitlement shall be credited as a lump sum on January 1. For mid-year joiners, entitlement shall be prorated: AnnualLeaveDays × remaining_days_in_year ÷ 365, rounded to the nearest 0.5 day. Minimum prorated entitlement is 0.5 days for any joiner with at least 1 remaining day in the year.
Priority: MUST
Roles: System behaviour

FR-34: Comp-off balance shall credit only when a Comp-Off Request is approved. The LeaveBalance table is the single source of truth for comp-off available balance; CompOffCredit stores individual credit entries for earn date and expiry date tracking.
Priority: MUST
Roles: System behaviour

FR-35: Comp-off credits shall expire 30 days from earn date (fixed, not configurable).
Priority: MUST
Roles: System behaviour

FR-36: A daily Hangfire job shall check and expire comp-off balances past their expiry date, decrementing LeaveBalance.balance and setting CompOffCredit.status = expired.
Priority: MUST
Roles: System behaviour

FR-37: An employee cannot submit a leave request if the balance for the selected leave type is zero or would go negative. Exception: Unpaid Leave is exempt from this check.
Priority: MUST
Roles: Employee, Manager

FR-38: A yearly Hangfire job running on December 31 shall lapse all unused balances (no carry-forward) and log each lapse in the audit trail.
Priority: MUST
Roles: System behaviour
```

### Domain: Leave Request & Workflow

```
FR-39: Employees and Managers shall be able to apply for leave by selecting: leave type, start date, end date, half-day flag (AM/PM), reason, and attachment (mandatory when RequiresAttachment=Yes).
Priority: MUST
Roles: Employee, Manager

FR-40: The system shall allow leave requests to be saved as drafts before submission.
Priority: MUST
Roles: Employee, Manager

FR-41: On submission, the system shall validate: sufficient balance (FR-37), no overlapping approved leave, team overlap limit not exceeded, start and end date are not weekends or public holidays.
Priority: MUST
Roles: System behaviour

FR-42: The system shall enforce the sandwich rule: for a leave request range, any non-working day (weekend/public holiday) that is connected through a contiguous chain of non-working days to at least one employee-selected leave day on each side within the range counts as a leave day. Non-working days bridging two separate requests are never counted.
Priority: MUST
Roles: System behaviour

FR-43: Team overlap limit shall be enforced per department (default max 2 employees on leave on the same date). Any approved leave — full-day or half-day — counts as 1 unit toward the limit. First-commit-wins at database level for concurrent submissions.
Priority: MUST
Roles: System behaviour

FR-44: An employee cannot submit two separate half-day leave requests for the same date. If a half-day exists for a date, the system shall block a second half-day for the other period and prompt the employee to modify the existing request to a full-day.
Priority: MUST
Roles: Employee, Manager

FR-45: Leave request status lifecycle: Draft → Submitted → Pending L1 Approval → L1 Approved → Pending L2 Approval (conditional) → Approved → Active (on start date) → Completed (on end date).
Priority: MUST
Roles: System behaviour

FR-46: An employee may cancel a submitted or approved leave request only before the start date.
Priority: MUST
Roles: Employee, Manager

FR-47: HR Admin may revoke an approved leave request only before the start date. On revocation, the full balance is restored.
Priority: MUST
Roles: HR Admin

FR-48: Retroactive leave requests (start date in the past) shall always require L2 (HR Admin) approval regardless of leave type or duration.
Priority: MUST
Roles: System behaviour

FR-49: Attachment upload shall accept PDF, JPG, PNG only, max 5MB, stored on local filesystem.
Priority: MUST
Roles: Employee, Manager

FR-50: On leave request submission, an email notification shall be sent to the employee's reporting manager (or HR Admin if no manager is linked).
Priority: MUST
Roles: System behaviour
```

### Domain: Comp-Off Requests

```
FR-51: Employees and Managers shall be able to submit a Comp-Off Request with fields: Date, Description, IsHalfDay, Start Time, End Time.
Priority: MUST
Roles: Employee, Manager

FR-52: The comp-off request date must be a public holiday or weekend.
Priority: MUST
Roles: System behaviour

FR-53: Worked hours shall be computed from Start Time and End Time. Half-day comp-off is allowed only if worked hours > 4; full-day is allowed only if worked hours ≥ 8; requests below 4 hours shall be blocked.
Priority: MUST
Roles: System behaviour

FR-54: Comp-Off Requests cannot be cancelled by the employee once submitted.
Priority: MUST
Roles: System behaviour

FR-55: Comp-Off Requests shall be approved or rejected by the employee's reporting manager (or HR Admin if no manager is linked). Rejection requires a mandatory reason.
Priority: MUST
Roles: Manager, HR Admin

FR-56: On approval, comp-off balance credits 0.5 day (half-day) or 1 day (full-day) with an expiry of earn_date + 30 days.
Priority: MUST
Roles: System behaviour

FR-57: Comp-off submission, approval, and rejection shall be recorded in the Audit Trail.
Priority: MUST
Roles: System behaviour
```

### Domain: Approval Engine

```
FR-58: L1 approval is required for all submitted leave requests. The L1 approver is the employee's reporting manager; if no manager is linked, HR Admin acts as L1. When HR Admin acts as L1, L2 is automatically skipped.
Priority: MUST
Roles: Manager, HR Admin

FR-59: L2 approval (HR Admin) is required when any of the following is true: leave duration > 3 consecutive days, RequiresHRFlag = Yes, or the request is retroactive (FR-48). L2 can only occur after L1 is completed.
Priority: MUST
Roles: HR Admin

FR-60: If a pending approval is not acted on within 2 days, the system shall send an escalation/reminder email to the pending approver. The daily Hangfire escalation job shall check all pending L1, L2, and comp-off requests.
Priority: MUST
Roles: System behaviour

FR-61: Approval/rejection shall trigger an immediate email notification to the employee.
Priority: MUST
Roles: System behaviour
```

### Domain: Public Holiday Calendar

```
FR-62: HR Admin shall manage public holidays with fields: date, name.
Priority: MUST
Roles: HR Admin, Super Admin

FR-63: The system shall support bulk import of holidays via CSV upload.
Priority: MUST
Roles: HR Admin, Super Admin

FR-64: Public holidays shall be factored into: sandwich rule calculation, working-day counting, team overlap detection, and comp-off eligibility.
Priority: MUST
Roles: System behaviour

FR-65: Employees cannot apply for leave on a public holiday or weekend — the leave application calendar shall disable these dates as start/end dates.
Priority: MUST
Roles: Employee, Manager
```

### Domain: Notifications

```
FR-66: The system shall send email notifications via SendGrid v3 for: leave applied, approved, rejected, cancelled, revoked, escalation reminders, comp-off applied, comp-off approved/rejected.
Priority: MUST
Roles: System behaviour

FR-67: If SendGrid is unavailable, emails shall be queued in Hangfire and retried 5 times over 24 hours. After all retries fail, the notification shall be marked "email delivery failed".
Priority: MUST
Roles: System behaviour

FR-68: An in-app notification center shall list all notifications with read/unread status. Clicking a notification navigates to the related leave or comp-off request.
Priority: MUST
Roles: All

FR-69: On leave approval (any leave type including Unpaid and Comp-off), the system shall create an all-day event in the employee's Google Calendar via a Hangfire job (3 retry attempts, exponential backoff). On cancel/revoke, the system shall delete the event.
Priority: MUST
Roles: System behaviour

FR-70: If Google Calendar sync fails after all retries, the failure shall be logged and the notification marked "calendar sync failed". The leave approval shall NOT be reversed.
Priority: MUST
Roles: System behaviour

FR-71: The frontend notification unread count shall be polled every 60 seconds.
Priority: MUST
Roles: All
```

### Domain: Reporting & Dashboards

```
FR-72: The Employee Dashboard (Employee, Manager) shall show: leave balance cards per active leave type (used/total with progress bar), quick action buttons (Apply for Leave, Request Comp-Off), recent 5 leave requests table, and upcoming public holidays for the next 30 days.
Priority: MUST
Roles: Employee, Manager

FR-73: The Manager Dashboard shall show: pending approvals count card (leave + comp-off) with link, team calendar (month view, direct reports), team balance summary table. Visible only when the manager has at least one subordinate.
Priority: MUST
Roles: Manager

FR-74: The HR Dashboard shall show: department-wise utilization bar chart, monthly leave trend line chart, policy compliance alert list, top cards (Total Leaves Today, Pending L2 Approvals, Policy Violations This Month).
Priority: MUST
Roles: HR Admin, Super Admin

FR-75: The Super Admin Dashboard shall show: Total Active Employees, Total Leaves Today (system-wide), Pending Approvals (all levels), Policy Violations This Month. Full audit log access.
Priority: MUST
Roles: Super Admin

FR-76: All reports shall support date range filtering. CSV export shall be available on all reports.
Priority: MUST
Roles: HR Admin, Super Admin
```

### Domain: Audit Trail

```
FR-77: The system shall log every state-changing action with: who performed it, what changed (old value → new value as JSON), when (timestamp), and from which IP address.
Priority: MUST
Roles: System behaviour

FR-78: Auditable actions shall include: leave request CRUD, approval/rejection, cancellation, revocation, comp-off CRUD and approval/rejection, leave type/policy changes, holiday changes, role changes, employee profile changes, department CRUD, account lock/unlock, login/logout.
Priority: MUST
Roles: System behaviour

FR-79: The audit log shall be append-only — no edits or deletes allowed.
Priority: MUST
Roles: System behaviour

FR-80: The audit log shall be searchable by: user, action type, date range, entity type.
Priority: MUST
Roles: HR Admin, Super Admin

FR-81: The audit log shall be retained for a minimum of 3 years.
Priority: MUST
Roles: System behaviour
```

### Domain: Initial Data Seeding

```
FR-82: On initial deployment, the system shall seed: one Super Admin user, one HR Admin user, one default department ("HR"), and 5 default leave types (Casual, Sick, Earned, Comp-off, Unpaid) — all with status Active.
Priority: MUST
Roles: System behaviour

FR-83: Both seeded accounts shall use password Admin@123 (meets policy FR-6) and local login only.
Priority: MUST
Roles: System behaviour

FR-84: The seed script shall be idempotent — re-running shall not create duplicate records.
Priority: MUST
Roles: System behaviour
```

---

## Section 5 — Acceptance Criteria

```
AC-1 (FR-1): GET /api/v1/auth/sso/login returns HTTP 302 redirect to the Azure AD authorization endpoint.

AC-2 (FR-1): GET /api/v1/auth/sso/callback with a valid authorization code returns HTTP 200 with access_token and sets a refresh token HttpOnly cookie.

AC-3 (FR-3): POST /api/v1/auth/login with valid email and password returns HTTP 200 with access_token and sets refresh token HttpOnly cookie.

AC-4 (FR-3): POST /api/v1/auth/login with invalid credentials returns HTTP 401.

AC-5 (FR-4): The returned JWT decodes to contain user_id, role, department_id, and exp fields.

AC-6 (FR-6): POST /api/v1/auth/login with password "abc123" (no uppercase) returns HTTP 422 with validation error.

AC-7 (FR-7): After 3 consecutive failed POST /api/v1/auth/login attempts, the 4th attempt returns HTTP 423 (Locked).

AC-8 (FR-8): GET /api/v1/accounts/locked by HR Admin returns HTTP 200 with list of locked accounts.

AC-9 (FR-8): POST /api/v1/accounts/{id}/unlock by HR Admin returns HTTP 200; subsequent login with that account succeeds.

AC-10 (FR-11): First SSO login by a user whose AD group has no configured mapping creates an account with role = Employee and status = Active.

AC-11 (FR-12): POST /api/v1/employees by HR Admin with valid payload returns HTTP 201 with the created employee record.

AC-12 (FR-15): POST /api/v1/employees by a Manager returns HTTP 403.

AC-13 (FR-16): DELETE /api/v1/employees/{id} by HR Admin sets the employee's status to Inactive; the employee record remains in the database.

AC-14 (FR-17): Saving an employee with reporting_manager_id = User X, where User X's current role is Employee, results in User X's role being set to Manager in the database.

AC-15 (FR-17): When the last direct report of User X is removed, User X's role is set back to Employee.

AC-16 (FR-18): Attempting to change a Manager's role to Employee while they have active direct reports returns HTTP 422 with message: "This user is a reporting manager for active employees and cannot be demoted to Employee."

AC-17 (FR-20): GET /api/v1/employees/team by a Manager with no direct reports returns HTTP 403 or empty list with no team menu rendered in the UI.

AC-18 (FR-23): POST /api/v1/departments with a name already used by an active department returns HTTP 422 with uniqueness error.

AC-19 (FR-24): DELETE /api/v1/departments/{id} with active employees assigned to it returns HTTP 422 with error indicating employees must be reassigned first.

AC-20 (FR-27): POST /api/v1/leave-types by HR Admin with valid payload returns HTTP 201 with the created leave type.

AC-21 (FR-31): GET /api/v1/balances/me returns HTTP 200 with an array of balance records — one per active leave type — containing total_entitled, used, balance fields.

AC-22 (FR-32): Approving a full-day leave request deducts exactly 1.0 day from the employee's balance for the selected leave type.

AC-23 (FR-32): Approving a half-day leave request deducts exactly 0.5 days from the employee's balance.

AC-24 (FR-33): A new employee joining on July 1 (184 remaining days) with Casual Leave (12 days annual) receives a prorated balance of 12 × 184 ÷ 365 = 6.05 → rounded to 6.0 days.

AC-25 (FR-35): A comp-off credit earned on July 1 has expiry_date = July 31.

AC-26 (FR-36): The daily CompOffExpiryJob sets CompOffCredit.status = expired and decrements LeaveBalance.balance for credits whose expiry_date ≤ today.

AC-27 (FR-37): POST /api/v1/leave-requests/submit by an employee with zero Casual Leave balance returns HTTP 422 with error "Insufficient balance."

AC-28 (FR-37): POST /api/v1/leave-requests/submit for Unpaid Leave by an employee with zero balance returns HTTP 200 (exempt from balance check).

AC-29 (FR-41): POST /api/v1/leave-requests/submit with start_date = Saturday returns HTTP 422 with error "Leave cannot start on a weekend."

AC-30 (FR-41): POST /api/v1/leave-requests/submit with start_date = a configured public holiday returns HTTP 422 with error "Leave cannot start on a public holiday."

AC-31 (FR-41): POST /api/v1/leave-requests/submit where the employee already has an approved leave on the same dates returns HTTP 422 with "Overlapping approved leave exists."

AC-32 (FR-42): Submitting Mon (Leave) + Tue (Holiday) + Wed (Leave) computes days_count = 2 (isolated holiday does not count).

AC-33 (FR-42): Submitting Thu (Holiday) + Fri (Leave) + Sat (Weekend) + Sun (Weekend) computes days_count = 4 (all days count via chain).

AC-34 (FR-43): When the team already has 2 approved leaves on a date (department overlap limit = 2), a third employee's submission for that date returns HTTP 422 with "Team overlap limit reached."

AC-35 (FR-44): Submitting a PM half-day leave for a date where the employee already has an approved AM half-day returns HTTP 422 with prompt to modify existing request to full-day.

AC-36 (FR-46): POST /api/v1/leave-requests/{id}/cancel with start_date in the past returns HTTP 422 with "Cannot cancel a leave that has already started."

AC-37 (FR-47): POST /api/v1/leave-requests/{id}/revoke by HR Admin for a leave with start_date in the future returns HTTP 200 and restores the employee's balance.

AC-38 (FR-47): POST /api/v1/leave-requests/{id}/revoke for a leave with start_date ≤ today returns HTTP 422.

AC-39 (FR-48): Submitting a leave with start_date = yesterday creates a request that enters Pending L1 Approval and has requires_l2 = true regardless of leave type.

AC-40 (FR-52): POST /api/v1/comp-off-requests with date = a working weekday returns HTTP 422 with "Comp-off date must be a weekend or public holiday."

AC-41 (FR-53): POST /api/v1/comp-off-requests with start_time = "09:00" and end_time = "12:00" (3 hours) returns HTTP 422 with "Worked hours insufficient for comp-off request."

AC-42 (FR-53): POST /api/v1/comp-off-requests with is_half_day = true and worked hours = 5 (> 4h) returns HTTP 201.

AC-43 (FR-53): POST /api/v1/comp-off-requests with is_half_day = false and worked hours = 10 (≥ 8h) returns HTTP 201.

AC-44 (FR-58): When an employee has no reporting manager, the pending approval for their leave request appears in HR Admin's GET /api/v1/approvals/pending and does NOT appear in any Manager's pending list.

AC-45 (FR-58): When HR Admin approves as L1 (no-manager case), the leave status moves directly to Approved without creating a Pending L2 step.

AC-46 (FR-59): A leave request for 5 consecutive days where L1 is approved moves to Pending L2 Approval automatically.

AC-47 (FR-59): A Sick Leave request (RequiresHRFlag = Yes) where L1 is approved moves to Pending L2 Approval.

AC-48 (FR-60): The EscalationJob sends an email to the pending approver for all requests where no action has been taken for ≥ 2 days.

AC-49 (FR-62): POST /api/v1/holidays by HR Admin with valid date and name returns HTTP 201.

AC-50 (FR-63): POST /api/v1/holidays/bulk-import with a valid CSV file returns HTTP 200 with count of holidays imported.

AC-51 (FR-65): In the Apply for Leave UI, weekends and dates matching the holiday list are disabled and cannot be selected as start/end dates.

AC-52 (FR-66): An email is sent to the reporting manager (or HR Admin) within 60 seconds of leave submission.

AC-53 (FR-67): When SendGrid returns 5xx, the email is re-queued in Hangfire and retried. After 5 retries, notification.email_status = "email delivery failed".

AC-54 (FR-68): GET /api/v1/notifications returns HTTP 200 with the user's notifications including read, title, message, related_entity_type, related_entity_id.

AC-55 (FR-69): On leave approval, a CalendarSyncJob is created in Hangfire and creates an all-day event in the employee's Google Calendar.

AC-56 (FR-70): When Google Calendar sync fails 3 times, notification.calendar_status = "calendar sync failed" and the leave status remains Approved.

AC-57 (FR-72): GET /api/v1/balances/me called after an approved leave is cancelled returns a balance that is 1.0 (or 0.5 for half-day) higher than before the cancellation.

AC-58 (FR-73): A Manager with no subordinates does not see the Manager Dashboard or Subordinate List in the UI navigation.

AC-59 (FR-74): GET /api/v1/reports/utilization by HR Admin returns HTTP 200 with per-department data including leave days used and entitled.

AC-60 (FR-76): GET /api/v1/reports/export with date_from and date_to parameters returns HTTP 200 with Content-Type: text/csv.

AC-61 (FR-77): Every leave request approval stores an AuditLog row with action = LEAVE_APPROVED, old_value = previous status JSON, new_value = new status JSON, user_id = approver, ip_address.

AC-62 (FR-79): Any attempt to DELETE from audit_log table returns HTTP 405 (or is blocked at the DB/application layer).

AC-63 (FR-80): GET /api/v1/audit-log?user_id=X&action=LEAVE_APPROVED&date_from=Y&date_to=Z returns HTTP 200 with filtered results.

AC-64 (FR-82): Running the seed script on an empty database creates exactly 1 Super Admin user, 1 HR Admin user, 1 HR department, and 5 leave types.

AC-65 (FR-84): Running the seed script twice results in the same 1+1+1+5 records — no duplicates.
```

---

## Section 6 — User Stories

```
US-1 (FR-1, FR-3, FR-4):
As an Employee, I want to log in via my Microsoft account (SSO) or local credentials so that I can access the leave management system securely.
Acceptance:
- AC-1: GET /api/v1/auth/sso/login returns HTTP 302 redirect to Azure AD.
- AC-2: SSO callback with valid code returns JWT and refresh token cookie.
- AC-3: Local login with valid credentials returns JWT and refresh token cookie.

US-2 (FR-7, FR-8):
As an HR Admin, I want to view and unlock accounts locked after repeated failed login attempts so that employees are not permanently locked out.
Acceptance:
- AC-7: 3 failed logins lock the account.
- AC-8: Locked accounts list returned to HR Admin.
- AC-9: Unlocking an account allows the user to log in again.

US-3 (FR-12, FR-13, FR-15, FR-17):
As an HR Admin, I want to create and manage employee profiles with validated entity links so that reporting structures drive the correct approval routing.
Acceptance:
- AC-11: Employee creation returns HTTP 201.
- AC-12: Manager cannot create employees (HTTP 403).
- AC-14: Employee creation with a reporting manager auto-promotes that manager's role.

US-4 (FR-19):
As an Employee, I want to view and edit my own profile (name and phone only) so that my contact information stays current.
Acceptance:
- AC-12 inverse: GET /api/v1/employees/me returns own profile.

US-5 (FR-21, FR-23, FR-24):
As an HR Admin, I want to manage departments with unique names and soft delete so that the department list stays accurate without losing historical data.
Acceptance:
- AC-18: Duplicate department name returns 422.
- AC-19: Deleting department with active employees returns 422.

US-6 (FR-31, FR-33):
As an Employee, I want to see my leave balances updated in real time so that I can make informed leave decisions.
Acceptance:
- AC-21: Balance endpoint returns one record per active leave type.
- AC-24: Mid-year joiner receives prorated balance.
- AC-57: Balance is restored after leave cancellation.

US-7 (FR-39, FR-40, FR-41, FR-42, FR-43, FR-65):
As an Employee, I want to apply for leave with real-time validation (balance, overlap, sandwich rule, team limit, working days) so that my submission is always valid.
Acceptance:
- AC-27: Zero balance blocks submission.
- AC-28: Unpaid Leave exempt from balance check.
- AC-29: Weekend start date blocks submission.
- AC-30: Holiday start date blocks submission.
- AC-31: Overlapping leave blocks submission.
- AC-32: Isolated holiday not counted in sandwich rule.
- AC-33: Chained non-working days counted via sandwich rule.
- AC-34: Team overlap limit enforced.

US-8 (FR-46, FR-47):
As an Employee, I want to cancel my leave request before it starts; as HR Admin, I want to revoke an approved leave before it starts — so that availability stays accurate.
Acceptance:
- AC-36: Cancel after start date returns 422.
- AC-37: Revoke before start date returns 200 and restores balance.
- AC-38: Revoke after start date returns 422.

US-9 (FR-51, FR-52, FR-53, FR-55, FR-56):
As an Employee, I want to submit a comp-off request for work done on a weekend/holiday so that I earn compensatory leave credited to my balance.
Acceptance:
- AC-40: Non-holiday/weekend date blocked.
- AC-41: < 4 worked hours blocked.
- AC-42: Half-day with > 4h approved.
- AC-43: Full-day with ≥ 8h approved.

US-10 (FR-58, FR-59, FR-60, FR-61):
As a Manager, I want to approve or reject L1 leave and comp-off requests for my direct reports, and as HR Admin, I want to handle L2 approvals — so that the approval chain is enforced correctly.
Acceptance:
- AC-44: No-manager case routes to HR Admin.
- AC-45: HR Admin L1 approval skips L2.
- AC-46: > 3 day leave triggers L2 after L1.
- AC-47: Sick Leave triggers L2 after L1.
- AC-48: Escalation reminder sent after 2 days of inaction.

US-11 (FR-62, FR-63, FR-64, FR-65):
As an HR Admin, I want to manage the public holiday calendar (including bulk CSV import) so that all leave validations and the calendar view reflect the correct working days.
Acceptance:
- AC-49: Holiday creation returns 201.
- AC-50: CSV bulk import returns count of imported holidays.
- AC-51: Calendar disables holiday dates as leave start/end.

US-12 (FR-66, FR-67, FR-68, FR-69, FR-70):
As an Employee, I want to receive email and in-app notifications for every leave/comp-off status change, and have approved leaves synced to Google Calendar so that I am always informed.
Acceptance:
- AC-52: Email sent within 60 seconds of submission.
- AC-53: Failed SendGrid delivery queued and retried.
- AC-54: Notification center returns all notifications.
- AC-55: Google Calendar event created on approval.
- AC-56: Calendar sync failure does not reverse approval.

US-13 (FR-72, FR-73, FR-74, FR-75, FR-76):
As each role, I want to see a role-appropriate dashboard with relevant leave data, charts, and export capability so that I have the visibility I need.
Acceptance:
- AC-57: Balance cards update after leave changes.
- AC-58: Manager with no subordinates has no Manager Dashboard in nav.
- AC-59: HR utilization report returns per-department data.
- AC-60: CSV export returns correct Content-Type.

US-14 (FR-77, FR-78, FR-79, FR-80):
As an HR Admin, I want to search and view a complete, tamper-proof audit trail of all system actions so that I can fulfill compliance obligations.
Acceptance:
- AC-61: Audit log row created on leave approval.
- AC-62: Audit log rows cannot be deleted.
- AC-63: Audit log search by user/action/date returns filtered results.

US-15 (FR-82, FR-83, FR-84):
As a Super Admin, I want the system to be pre-seeded with default users, department, and leave types on first deployment so that the system is operational immediately.
Acceptance:
- AC-64: Seed creates correct default records.
- AC-65: Re-running seed does not create duplicates.
```

---

## Section 7 — Non-Functional Requirements

```
NFR-1: API CRUD endpoints shall respond within 200ms at p50 and 500ms at p99.
Category: Performance
Measurable target: p50 < 200ms, p99 < 500ms under 500 concurrent users

NFR-2: Report generation endpoints shall respond within 2 seconds at p99.
Category: Performance
Measurable target: p99 < 2000ms for /reports/* endpoints

NFR-3: The system shall support 500 concurrent authenticated users without degradation.
Category: Scalability
Measurable target: 500 concurrent sessions with < 5% error rate

NFR-4: System uptime shall be 99.9% (approximately 8.7 hours downtime per year).
Category: Availability
Measurable target: 99.9% monthly uptime measured by health check endpoint

NFR-5: All API endpoints (except /health and /api/v1/auth/*) shall require valid JWT authentication.
Category: Security
Measurable target: 100% of protected endpoints return HTTP 401 for unauthenticated requests

NFR-6: All API endpoints shall enforce RBAC per the Permission Matrix in docs/06_rbac.md.
Category: Security
Measurable target: 100% of endpoints return HTTP 403 for unauthorized role access

NFR-7: API rate limiting shall be enforced at 100 requests per minute per authenticated user.
Category: Security
Measurable target: 101st request within 1 minute returns HTTP 429

NFR-8: Passwords and tokens shall be encrypted at rest and never appear in logs.
Category: Security
Measurable target: 0 plaintext passwords or tokens in application logs (verified by log audit)

NFR-9: All employee PII shall be exportable on request per GDPR Article 15.
Category: Compliance
Measurable target: Export endpoint returns all PII fields for a given employee within 72 hours of request

NFR-10: Employee data shall be soft-deletable with anonymization option per GDPR Article 17.
Category: Compliance
Measurable target: Anonymization sets name = "Anonymized", email = "anon-{id}@deleted.local", phone = null

NFR-11: The system shall operate in IST (UTC+5:30) timezone for all business logic.
Category: Compliance
Measurable target: All date comparisons (sandwich rule, holiday check, expiry) use IST

NFR-12: The leave year shall be January 1 to December 31.
Category: Compliance
Measurable target: Year-end lapse job runs on Dec 31; new-year credit job runs on Jan 1
```

---

## Section 8 — Data Model (Conceptual)

```
Entity: User
Fields: id (UUID, PK), email (string, unique), name (string), phone (string, nullable),
        department_id (UUID, FK), designation (string), date_of_joining (date),
        reporting_manager_id (UUID, FK self-ref, nullable), role (enum: Employee/Manager/HR Admin/Super Admin),
        status (enum: Active/Inactive), failed_login_attempts (int), locked_at (datetime, nullable),
        password_hash (string, nullable), created_at, updated_at
Relationships: belongs_to Department; self-ref to User (reporting manager); has_many LeaveRequests, LeaveBalances, CompOffRequests, Notifications, AuditLogs
RBAC: HR Admin/Super Admin = CRUD; Manager = read own team; Employee = read/edit own (name, phone)

Entity: Department
Fields: id (UUID, PK), name (string, unique CI), code (string, unique CI), team_overlap_limit (int, default 2), status (enum: Active/Inactive), created_at, updated_at
Relationships: has_many Users
RBAC: HR Admin/Super Admin = CRUD; Manager/Employee = read

Entity: LeaveType
Fields: id (UUID, PK), name (string), code (string, unique), description (string), annual_leave_days (decimal), requires_attachment (bool), requires_hr_flag (bool), is_active (bool), created_at, updated_at
Relationships: has_many LeaveBalances, LeaveRequests
RBAC: HR Admin/Super Admin = CRUD; All = read

Entity: LeaveBalance
Fields: id (UUID, PK), user_id (UUID, FK), leave_type_id (UUID, FK), year (int), total_entitled (decimal), used (decimal), balance (decimal), created_at, updated_at
Relationships: belongs_to User, belongs_to LeaveType
RBAC: Employee/Manager = read own; Manager = read team; HR Admin/Super Admin = read all

Entity: LeaveRequest
Fields: id (UUID, PK), user_id (UUID, FK), leave_type_id (UUID, FK), start_date (date), end_date (date), half_day (bool), half_day_period (enum: AM/PM, nullable), days_count (decimal), reason (text), attachment_path (string, nullable), status (enum: Draft/Submitted/PendingL1/L1Approved/PendingL2/Approved/Active/Completed/Rejected/Cancelled/Revoked), applied_at (datetime), approved_at (datetime, nullable), cancelled_at (datetime, nullable), revoked_at (datetime, nullable), created_at, updated_at
Relationships: belongs_to User, belongs_to LeaveType, has_many ApprovalSteps
RBAC: Employee/Manager = CRUD own (submit, cancel); Manager = read own team; HR Admin/Super Admin = read all + revoke

Entity: ApprovalStep
Fields: id (UUID, PK), leave_request_id (UUID, FK), approver_id (UUID, FK), level (enum: L1/L2), status (enum: Pending/Approved/Rejected), acted_at (datetime, nullable), comments (text, nullable), last_reminder_sent (datetime, nullable), created_at
Relationships: belongs_to LeaveRequest, belongs_to User (approver)
RBAC: Manager = L1 own team; HR Admin = L2; read by owner + approver

Entity: CompOffRequest
Fields: id (UUID, PK), user_id (UUID, FK), date (date), description (text), is_half_day (bool), start_time (time), end_time (time), worked_hours (decimal), status (enum: Pending/Approved/Rejected), approver_id (UUID, FK, nullable), acted_at (datetime, nullable), rejection_reason (text, nullable), created_at, updated_at
Relationships: belongs_to User, belongs_to User (approver), has_many CompOffCredits
RBAC: Employee/Manager = create own; Manager/HR Admin = approve/reject

Entity: CompOffCredit
Fields: id (UUID, PK), user_id (UUID, FK), comp_off_request_id (UUID, FK), earned_date (date), expiry_date (date), days (decimal: 0.5 or 1.0), status (enum: Active/Used/Expired), created_at
Relationships: belongs_to User, belongs_to CompOffRequest
RBAC: read by owner, Manager, HR Admin, Super Admin

Entity: Holiday
Fields: id (UUID, PK), date (date, unique per year), name (string), year (int), created_at, updated_at
Relationships: none
RBAC: All = read; HR Admin/Super Admin = CRUD

Entity: Notification
Fields: id (UUID, PK), user_id (UUID, FK), type (string), title (string), message (text), read (bool), related_entity_type (string), related_entity_id (UUID, nullable), email_status (enum: Pending/Sent/Failed), calendar_status (enum: Pending/Synced/Failed/NA), created_at
Relationships: belongs_to User
RBAC: read/update own only

Entity: AuditLog
Fields: id (UUID, PK), user_id (UUID, FK), action (string), entity_type (string), entity_id (UUID), old_value (JSONB), new_value (JSONB), ip_address (string), created_at
Relationships: belongs_to User
RBAC: HR Admin/Super Admin = read; no updates or deletes permitted
```

---

## Section 9 — API Surface (Conceptual)

```
POST /api/v1/auth/login
Purpose: Local email+password login
Auth: None
Request: { email, password }
Response: { access_token, expires_in } + HttpOnly refresh token cookie
Errors: 401 (invalid credentials), 423 (account locked)

GET /api/v1/auth/sso/login
Purpose: Redirect to Azure AD authorization endpoint
Auth: None
Response: 302 redirect

GET /api/v1/auth/sso/callback
Purpose: Exchange authorization code for JWT
Auth: None
Request: ?code=...&state=...
Response: { access_token } + HttpOnly refresh token cookie
Errors: 400 (invalid code)

POST /api/v1/auth/refresh
Purpose: Refresh JWT access token
Auth: HttpOnly cookie (refresh token)
Response: { access_token }
Errors: 401 (expired/invalid refresh token)

POST /api/v1/auth/logout
Purpose: Invalidate refresh token in DB
Auth: Bearer JWT
Response: 204

GET /api/v1/employees
Purpose: List employees with filters
Auth: HR Admin, Super Admin
Request: ?department_id=&role=&status=&page=&limit=
Response: { data: [User], total, page }

GET /api/v1/employees/me
Purpose: Own employee profile
Auth: All roles
Response: User object

GET /api/v1/employees/team
Purpose: Own direct reports
Auth: Manager (only if has subordinates)
Response: [User]
Errors: 403 (no subordinates or not Manager)

POST /api/v1/employees
Purpose: Create employee
Auth: HR Admin, Super Admin
Request: { name, email, department_id, designation, date_of_joining, reporting_manager_id?, status }
Response: 201 + User
Errors: 422 (validation), 409 (email conflict)

PUT /api/v1/employees/{id}
Purpose: Update employee
Auth: HR Admin, Super Admin; Employee (own: name/phone only)
Response: 200 + User
Errors: 403, 422

DELETE /api/v1/employees/{id}
Purpose: Soft delete (set Inactive)
Auth: HR Admin, Super Admin
Response: 204
Errors: 403

POST /api/v1/leave-requests
Purpose: Create leave request (draft or submit)
Auth: Employee, Manager
Request: { leave_type_id, start_date, end_date, half_day, half_day_period?, reason, attachment? }
Response: 201 + LeaveRequest
Errors: 422 (balance, overlap, team limit, weekend/holiday start/end)

POST /api/v1/leave-requests/{id}/submit
Purpose: Submit a draft leave request
Auth: Owner
Response: 200 + LeaveRequest (status = PendingL1)
Errors: 422 (validation failures)

POST /api/v1/leave-requests/{id}/cancel
Purpose: Cancel a leave request (before start date)
Auth: Owner
Response: 200
Errors: 422 (already started or past)

POST /api/v1/leave-requests/{id}/revoke
Purpose: Revoke an approved leave (HR Admin, before start date)
Auth: HR Admin, Super Admin
Request: { reason }
Response: 200
Errors: 422 (leave already started), 403

GET /api/v1/approvals/pending
Purpose: List pending L1/L2 approvals
Auth: Manager (own team), HR Admin (L2), Super Admin
Response: [LeaveRequest with approval step]

POST /api/v1/approvals/{request_id}/approve
Purpose: Approve L1 or L2
Auth: Manager (L1 own team), HR Admin (L2)
Response: 200
Errors: 403, 422 (wrong level or already acted)

POST /api/v1/approvals/{request_id}/reject
Purpose: Reject L1 or L2
Auth: Manager (L1 own team), HR Admin (L2)
Request: { reason }
Response: 200
Errors: 403, 422

POST /api/v1/comp-off-requests
Purpose: Submit comp-off request
Auth: Employee, Manager
Request: { date, description, is_half_day, start_time, end_time }
Response: 201 + CompOffRequest
Errors: 422 (non-holiday/weekend date, insufficient hours)

POST /api/v1/comp-off-requests/{id}/approve
Purpose: Approve comp-off request
Auth: Manager (own team), HR Admin (no-manager employees)
Response: 200
Errors: 403

POST /api/v1/holidays/bulk-import
Purpose: Bulk import holidays via CSV
Auth: HR Admin, Super Admin
Request: multipart/form-data (CSV file)
Response: 200 + { imported: N }
Errors: 422 (invalid CSV format)

GET /api/v1/reports/export
Purpose: CSV export of leave data
Auth: HR Admin, Super Admin
Request: ?date_from=&date_to=&department_id=&leave_type_id=
Response: 200, Content-Type: text/csv

GET /api/v1/audit-log
Purpose: Search audit trail
Auth: HR Admin, Super Admin
Request: ?user_id=&action=&entity_type=&date_from=&date_to=&page=&limit=
Response: { data: [AuditLog], total, page }
```

---

## Section 10 — Integration Points

```
Integration: Azure AD (Microsoft Entra ID)
Purpose: SSO login via OAuth2 Authorization Code Flow; AD group membership → LMS role mapping
Direction: Outbound (browser redirects to Azure AD, callback returns to LMS)
Auth method: OAuth2 Authorization Code + PKCE; client credentials (client_id + client_secret)
Failure handling: If Azure AD is unreachable, local login remains fully operational as fallback

Integration: SendGrid
Purpose: Transactional email notifications for all leave/comp-off status changes and escalation reminders
Direction: Outbound
Auth method: API Key (v3)
Failure handling: Hangfire retry queue — 5 attempts with exponential backoff over 24 hours. After all retries: notification.email_status = "email delivery failed". Does not block any leave operation.

Integration: Google Calendar API v3
Purpose: Create all-day events on leave approval; delete events on cancel/revoke
Direction: Outbound (per-user OAuth2 consent required)
Auth method: OAuth2 per user (user grants calendar access on first use)
Failure handling: Hangfire retry — 3 attempts with exponential backoff. After 3 failures: notification.calendar_status = "calendar sync failed", error logged. Leave approval is NEVER reversed due to calendar sync failure.

Integration: PostgreSQL 15+
Purpose: Primary data store (all application data + Hangfire job persistence)
Direction: Internal
Auth method: Connection string (username + password)
Failure handling: Application returns 503; Hangfire jobs resume on DB recovery

Integration: Hangfire (background job engine)
Purpose: Async email dispatch, Google Calendar sync, escalation reminders, comp-off expiry, year-end balance lapse, new-year credit
Direction: Internal
Auth method: PostgreSQL connection string
Failure handling: Jobs are persistent in PostgreSQL; missed jobs re-execute on scheduler recovery
```

---

## Section 11 — Security Requirements

```
SEC-1: All endpoints except /health and /api/v1/auth/* shall require a valid JWT Bearer token.
Category: Auth
Implementation note: ASP.NET Core [Authorize] on all controllers; anonymous allowed only for auth + health routes.

SEC-2: Role-based access control shall be enforced on every controller action per docs/06_rbac.md.
Category: Auth
Implementation note: [Authorize(Roles = "...")] or custom policy on every endpoint. Scoped queries for Manager endpoints.

SEC-3: JWT signing key shall be minimum 32 characters, stored as environment variable, never in source code.
Category: Secrets
Implementation note: Jwt__Secret env var; HS256 algorithm minimum.

SEC-4: Refresh tokens shall be stored in the database (hashed), transmitted only via HttpOnly SameSite=Strict cookie.
Category: Auth
Implementation note: Refresh token value hashed with SHA-256 before DB storage. Cookie: HttpOnly, Secure, SameSite=Strict.

SEC-5: Local passwords shall be hashed with BCrypt (minimum work factor 12) before storage.
Category: Data
Implementation note: BCrypt.Net-Next with cost factor 12. Never log plaintext passwords.

SEC-6: All database connections shall use TLS.
Category: Network
Implementation note: Npgsql connection string with SSL mode = Require.

SEC-7: File uploads shall be validated for MIME type (PDF/JPG/PNG only) and size (max 5MB) before storage.
Category: Data
Implementation note: Server-side validation only; do not trust client-supplied Content-Type.

SEC-8: API rate limiting shall be enforced at 100 requests per minute per authenticated user.
Category: Network
Implementation note: ASP.NET Core rate limiting middleware; return HTTP 429.

SEC-9: All employee PII shall support export (GDPR Article 15) and anonymization on deletion request (GDPR Article 17).
Category: Compliance
Implementation note: Export endpoint returns all User fields. Anonymization: name = "Anonymized", email = "anon-{id}@deleted.local", phone = null, status = Inactive.

SEC-10: Audit trail rows shall never be updated or deleted — enforced at application and database layer.
Category: Compliance
Implementation note: No UPDATE/DELETE on audit_logs table. Repository throws if update/delete attempted. DB-level: consider row-level security or trigger to block.

SEC-11: Azure AD client secret, SendGrid API key, and Google Calendar client secret shall never appear in source code or logs.
Category: Secrets
Implementation note: All secrets via environment variables; Serilog configured to destructure and redact sensitive fields.

SEC-12: CORS shall be configured to allow only the known frontend origin(s).
Category: Network
Implementation note: Cors__AllowedOrigins env var; default allow-credentials policy.
```

---

## Section 12 — Constraints and Assumptions

### Technical Constraints
- C1: Must use ASP.NET Core (.NET 8) + PostgreSQL (existing team expertise, non-negotiable)
- C2: Must integrate with Azure AD (corporate standard SSO)
- C3: Audit trail minimum 3-year retention (regulatory requirement)
- C4: No permanent employee data deletion without anonymization (GDPR Article 17)
- C5: Attachment storage on local filesystem (Phase 1); S3 migration planned for Phase 2

### Business Constraints
- Single-organization deployment (no multi-tenancy)
- IST timezone only (Phase 1)
- Leave year: January 1 to December 31 (fixed, not configurable)
- Sandwich rule is mandatory and not configurable
- Comp-off expiry is fixed at 30 days (not configurable)

### Assumptions
- A1: Maximum 500 concurrent users
- A2: English only; no localization required
- A3: All employees are salaried (no hourly/contract leave rules)
- A4: Weekend is Saturday and Sunday for all employees (no flexible work schedules)
- A5: Single holiday list applies to the whole organization (no regional variants)
- A6: Azure AD tenant exists and an app registration can be created
- A7: SendGrid account with dynamic template support is available
- A8: Google Calendar OAuth2 app registration is available

---

## Section 13 — Out of Scope (Detailed)

| Item | Reason Excluded | What Would Change It |
|------|----------------|---------------------|
| Mobile application | Outside Phase 1 budget/timeline | Phase 2 funding decision |
| Payroll integration | Requires payroll system API access (not yet contracted) | Phase 2 + payroll vendor contract |
| S3 attachment storage | Local filesystem sufficient for Phase 1 scale | Phase 2 when file volume grows |
| Multi-timezone | Single org, IST only; adds significant complexity | Phase 2 if org goes global |
| Outlook calendar | Only Google Calendar in Phase 1; Outlook requires separate integration | Phase 2 if user feedback warrants |
| PDF report export | CSV sufficient for Phase 1 | Phase 2 based on stakeholder request |
| Carry-forward balances | Policy decision: no carry-forward in this org | Policy change by HR leadership |
| Configurable comp-off expiry | Fixed at 30 days per HR policy | HR policy change |
| Bulk approve/reject | Not required by managers in current process | Future usability feedback |
| Leave for HR Admin/Super Admin | HR Admin and Super Admin are system operators, not leave users | Architectural decision — not planned |
| Admin UI for Azure AD role mapping | Low-frequency config change; app config is sufficient | Phase 2 if mapping changes frequently |
| Past data migration | Existing data is in spreadsheets; format inconsistencies make migration high-risk | Out of Phase 1 scope; manual entry if needed |

---

## Section 14 — Open Questions

```
Q-1: What is the exact Azure AD tenant ID and client ID for the app registration?
Owner: HR / IT Admin
Impact if unresolved: SSO cannot be configured or tested; local login works as fallback
Status: DEFERRED (will be provided at environment setup — HIL 7)

Q-2: What are the SendGrid API key and dynamic template IDs for each notification type?
Owner: IT Admin / Email team
Impact if unresolved: Email notifications cannot be sent; leave workflow still functional
Status: DEFERRED (will be provided at environment setup — HIL 7)

Q-3: What is the Google Calendar OAuth2 client ID and secret?
Owner: IT Admin / Google Workspace admin
Impact if unresolved: Google Calendar sync cannot be enabled; leave approval not blocked
Status: DEFERRED (will be provided at environment setup — HIL 7)

Q-4: What are the specific AD group names that map to each LMS role?
Owner: IT Admin / HR
Impact if unresolved: SSO users will default to Employee role; manual role assignment workaround via HR Admin
Status: DEFERRED

Q-5: Is the Hangfire dashboard (/hangfire) to be accessible in production, and if so, with what authentication?
Owner: Super Admin / IT team
Impact if unresolved: Dashboard defaults to disabled in production; no job monitoring UI
Status: OPEN

Q-6: What timezone does the server run in? (IST must be the business logic timezone, but the OS may differ.)
Owner: IT Admin / DevOps
Impact if unresolved: Hangfire scheduled jobs (Dec 31 lapse, Jan 1 credit, daily escalation) may run at wrong times
Status: OPEN

Q-7: Should the report export include personally identifiable information (PII), or should it be anonymized?
Owner: HR / Legal
Impact if unresolved: CSV export may inadvertently expose PII; default to include name + email for HR use
Status: OPEN
```

---

## Section 15 — E2E Test Scenarios (Playwright)

```
PT-1 (FR-1, FR-3, FR-4, AC-3):
Scenario: Employee logs in with local credentials
Actor: Employee
Steps:
  1. Navigate to /auth/login
  2. Enter valid email and password in local login form
  3. Click "Sign In"
  4. Wait for redirect to /me/dashboard
Expected: Employee Dashboard is visible; navbar shows employee name and unread notification badge

PT-2 (FR-1, FR-4, AC-1, AC-2):
Scenario: User logs in via Azure AD SSO
Actor: Employee
Steps:
  1. Navigate to /auth/login
  2. Click "Sign in with Microsoft"
  3. Complete Azure AD authentication in the popup/redirect
  4. System receives SSO callback and redirects to /me/dashboard
Expected: Employee Dashboard is visible; JWT issued and stored in memory

PT-3 (FR-7, FR-8, AC-7, AC-9):
Scenario: Account lock and HR Admin unlock flow
Actor: Employee (lockee), HR Admin (unlocker)
Steps:
  1. Employee: attempt login 3 times with wrong password
  2. Employee: 4th login attempt shows "Account locked" message
  3. HR Admin: navigate to /hr/locked-accounts
  4. HR Admin: find the locked account and click Unlock
  5. HR Admin: confirm the unlock dialog
  6. Employee: attempt login with correct password
Expected: Employee successfully reaches /me/dashboard after unlock

PT-4 (FR-39, FR-41, FR-42, FR-43, AC-27, AC-29, AC-33):
Scenario: Employee applies for leave with full validation flow
Actor: Employee
Steps:
  1. Navigate to /leave/apply
  2. Select leave type "Casual Leave"
  3. Select start_date = next Thursday (holiday), end_date = next Friday
  4. Observe: days_count shows sandwich rule calculation including Thursday holiday
  5. Observe: balance card shows available balance
  6. Click Submit
  7. Observe success toast and redirect to /leave/history
Expected: Leave request created with status Pending L1 Approval; balance card is not yet deducted (deducted on approval)

PT-5 (FR-37, AC-27):
Scenario: Employee is blocked when balance is zero
Actor: Employee
Steps:
  1. Navigate to /leave/apply (all Casual Leave used)
  2. Select leave type "Casual Leave"
  3. Select valid date range
  4. Observe: Submit button is disabled with message "Insufficient balance"
Expected: Submit button disabled; no request created

PT-6 (FR-58, FR-59, AC-44, AC-45):
Scenario: Leave approval — employee with no reporting manager (HR Admin as L1 + L2 skip)
Actor: Employee (no manager), HR Admin
Steps:
  1. Employee: submit leave request (any type, ≤ 3 days)
  2. HR Admin: navigate to /approvals and see the request in pending list
  3. HR Admin: approve the request
Expected: Leave status moves directly to Approved (no Pending L2 created); balance deducted; email sent to employee

PT-7 (FR-58, FR-59, AC-46, AC-47):
Scenario: Two-level approval for Sick Leave
Actor: Employee, Manager (L1), HR Admin (L2)
Steps:
  1. Employee: submit Sick Leave request with attachment
  2. Manager: navigate to /approvals; see L1 pending request; approve
  3. Check leave status = L1 Approved → Pending L2
  4. HR Admin: navigate to /approvals; see L2 pending request; approve
Expected: Leave status = Approved; balance deducted; email sent to employee at each approval step

PT-8 (FR-51, FR-52, FR-53, FR-55, FR-56, AC-42, AC-43):
Scenario: Employee submits comp-off request for holiday work and manager approves
Actor: Employee, Manager
Steps:
  1. Employee: navigate to /leave/comp-off
  2. Employee: select date = next Saturday (weekend), description, start_time = "09:00", end_time = "18:00" (9 hours), is_half_day = false
  3. Employee: submit
  4. Manager: navigate to /approvals → Comp-Off Requests tab; approve
  5. Employee: navigate to /leave/balances; check Comp-Off balance increased by 1 day
Expected: CompOffCredit created with expiry = earn_date + 30 days; LeaveBalance.balance for Comp-off incremented by 1.0

PT-9 (FR-46, FR-47, AC-36, AC-37):
Scenario: Employee cancels a leave; HR Admin tries to revoke a started leave (blocked)
Actor: Employee, HR Admin
Steps:
  1. Employee: submit and get approved a future leave request
  2. Employee: navigate to /leave/history; click Cancel; confirm
  3. Check: balance restored
  4. (Separate test) HR Admin: try to revoke a leave whose start_date = today
  5. Check: revoke returns error "Cannot revoke a leave that has already started"
Expected: Cancel restores balance; revoke after start blocked

PT-10 (FR-62, FR-63, AC-49, AC-50, AC-51):
Scenario: HR Admin manages holidays and calendar disables them in leave application
Actor: HR Admin, Employee
Steps:
  1. HR Admin: navigate to /hr/holidays/manage; add holiday for next Monday
  2. HR Admin: also bulk import a CSV with 3 additional holidays
  3. Employee: navigate to /leave/apply; observe next Monday is disabled in date picker
  4. Employee: observe CSV-imported holidays are also disabled
Expected: 4 new holidays visible in holiday list; all 4 disabled in leave application calendar

PT-11 (FR-66, FR-68, FR-69, AC-52, AC-54, AC-55):
Scenario: Leave approval triggers email, in-app notification, and Google Calendar event
Actor: Employee, Manager
Steps:
  1. Employee: submit leave request
  2. Manager: approve L1 (no L2 needed)
  3. Check employee's notification center: "Leave Approved" notification present
  4. Check employee's Google Calendar: all-day event created for the leave dates
  5. Check email inbox (or test email intercept): approved email received
Expected: All 3 notification channels triggered within 2 minutes of approval

PT-12 (FR-72, FR-73, FR-74, FR-75):
Scenario: Role-appropriate dashboards visible
Actor: Employee, Manager, HR Admin, Super Admin
Steps:
  1. Login as Employee → verify /me/dashboard shows balance cards and recent history
  2. Login as Manager (with subordinates) → verify /manager/dashboard visible with team calendar
  3. Login as HR Admin → verify /hr/dashboard shows utilization chart and trend chart
  4. Login as Super Admin → verify /admin/dashboard shows system-wide metrics
Expected: Each role sees exactly their designated dashboard; restricted screens return 403 or are absent from nav

PT-13 (FR-77, FR-80, AC-61, AC-63):
Scenario: Audit log records leave approval and is searchable by HR Admin
Actor: Manager (approves), HR Admin (views audit)
Steps:
  1. Manager: approve a leave request
  2. HR Admin: navigate to /hr/audit-log
  3. HR Admin: filter by action = LEAVE_APPROVED and today's date
  4. Observe: audit row visible with old_value, new_value, approver name, timestamp, IP
Expected: Audit row present within 30 seconds of approval action

PT-14 (FR-82, FR-83, FR-84, AC-64, AC-65):
Scenario: Initial seed creates default data idempotently
Actor: Super Admin (verifies)
Steps:
  1. Run seed script on clean database
  2. Login as Super Admin (Admin@123)
  3. Check Employee List: Super Admin and HR Admin accounts visible
  4. Check Department List: "HR" department visible
  5. Check Leave Types: all 5 default types visible and active
  6. Run seed script again
  7. Re-check counts — no duplicates
Expected: Same 2+1+5 records after second seed run
```

---

## Section 16 — HITL Review Checkpoint ⛔

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
FORGE  HIL 1 — PRD REVIEW
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Requirements summary:
- 84 Functional Requirements (FR-1 to FR-84)
- 65 Acceptance Criteria (AC-1 to AC-65)
- 14 Playwright Scenarios (PT-1 to PT-14)
- 7 Open Questions (Q-1 to Q-7; Q-1 to Q-4 DEFERRED to HIL 7 — do not block Phase 1)

Review checklist:
□ Every MUST feature has at least one FR-
□ Every MUST FR has at least one AC-
□ Every AC is binary (pass/fail)
□ Every critical user journey has a PT-
□ Scope boundary is clear
□ Open Questions Q-1 to Q-4 are deferred to HIL 7 (dependency gate); Q-5 to Q-7 need owner resolution before build

Type YES to confirm the PRD and proceed to /constitution.
Type REVISE [section] to update specific sections.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```
