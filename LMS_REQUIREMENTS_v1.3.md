# Leave Management System — Requirements Document

**Version**: 1.3
**Date**: July 2026
**Status**: Approved for Phase 1 Development

---

## 1. Executive Summary

The Leave Management System (LMS) is an enterprise web application that replaces manual leave tracking (spreadsheets, emails, verbal approvals) with an automated, policy-driven system. It handles the full leave lifecycle — from employee application through multi-level approval, balance tracking, and compliance reporting.

The system integrates with Azure AD for single sign-on, SendGrid for email notifications, and Google Calendar for leave event synchronization.

---

## 2. Business Objectives

- Eliminate manual leave tracking and approval via email/spreadsheets
- Enforce company leave policies automatically (balance limits, mandatory sandwich rule, weekend/holiday restrictions)
- Provide real-time visibility into team availability for managers
- Reduce approval bottlenecks through automated reminders and escalation emails
- Maintain auditable records of all leave transactions for compliance
- Enable HR to manage leave types, holidays, and reporting from a single dashboard

---

## 3. Users & Roles

### 3.1 Role Definitions

| Role | Description | Assigned By |
|------|-------------|-------------|
| **Employee** | Any staff member who applies for leave, views own balance and history | Auto-assigned on account creation |
| **Manager** | Team lead who approves/rejects direct reports' leave and comp-off requests, views team calendar | Auto-assigned based on reporting structure in employee profile |
| **HR Admin** | HR team member who manages leave types, holidays, employees, reporting; handles L2 approvals and revocations; acts as approver for employees with no reporting manager | Assigned by Super Admin |
| **Super Admin** | System administrator with full access — manages all data and audit logs | Assigned during system setup |

### 3.2 Role Hierarchy

```
Super Admin
  └── HR Admin
       └── Manager
            └── Employee
```

Higher roles inherit administrative permissions of lower roles plus additional capabilities. **Exception (v1.1):** personal leave screens (Apply for Leave, Employee Dashboard, My Leave Balances, My Leave History, My Profile, Cancel Leave Request, Comp-Off Request) are visible only to Employee and Manager roles — see Screen Visibility Matrix (10.3).

### 3.3 Reporting Structure

- An Employee **may** have one reporting Manager (stored in employee profile). The reporting manager link is not mandatory.
- If an employee has **no reporting manager linked**, all leave/comp-off notifications and L1 approvals for that employee shall route to **HR Admin**.
- Every Manager is also an Employee (can apply for leave themselves); a Manager's leave is approved by the Manager's own reporting manager (or HR Admin if none is linked).
- A Manager or Employee shall see the list of their juniors/subordinates **only if at least one employee reports to them**; otherwise the team/subordinate views are hidden.
- **HR Admin and Super Admin cannot apply for leave in the system.** Leave application and all personal leave screens are available to Employee and Manager roles only.

---

## 4. Functional Requirements

### 4.1 Authentication & Single Sign-On

| ID | Requirement |
|----|-------------|
| AUTH-01 | System shall support Azure AD SSO via OAuth2 authorization code flow |
| AUTH-02 | Azure AD groups shall map to internal roles (configurable mapping: AD group name → LMS role, maintained in application configuration — no admin UI in Phase 1). If a new SSO user's AD group does not match any configured mapping, the system shall assign the **Employee** role by default and create the account as active |
| AUTH-03 | System shall support local email + password login as fallback for non-SSO environments |
| AUTH-04 | After SSO or local login, system shall issue JWT access token (24h expiry) and refresh token (7d expiry) |
| AUTH-05 | Refresh tokens shall be stored in the database and invalidated on logout |
| AUTH-06 | Local login passwords shall meet policy: minimum 8 characters, at least 1 uppercase letter, at least 1 number |
| AUTH-07 | Account shall lock after **3** consecutive failed local login attempts |
| AUTH-08 | Locked accounts shall be viewable and unlockable by HR Admin or Super Admin only, via the Locked Account Management screen (SCR-22) |
| AUTH-09 | JWT shall contain: user_id, role, department_id (for fast RBAC checks without DB lookup) |
| AUTH-10 | Role changes shall take effect on next token refresh (not immediately mid-session) |

### 4.2 Employee Management

| ID | Requirement |
|----|-------------|
| EMP-01 | Employee profile shall contain: name, email, department, designation, date of joining, reporting manager, status (active/inactive) |
| EMP-02 | Wherever a screen links one entity to another (e.g. Department and Reporting Manager on the Employee form, Leave Type on the leave request form), the UI shall present a **dropdown** populated with active records of the linked entity — no free-text entry of linked references |
| EMP-03 | Reporting manager is optional. If not linked, the employee's leave and comp-off notifications/approvals route to HR Admin (see 3.3) |
| EMP-04 | Only **HR Admin and Super Admin** can add, edit, or delete (soft delete) employees. Manager relationship is editable by HR Admin and Super Admin only |
| EMP-05 | Employee profile shall support soft delete (set inactive, retain data for audit) |
| EMP-06 | The Manager role is derived automatically from the reporting structure and cannot be manually assigned or removed: (a) When an employee record is saved (create or edit) with a reporting_manager_id set to User X, the system shall immediately set User X's role to Manager if it is currently Employee. (b) When the last employee reporting to User X is removed or reassigned, the system shall automatically downgrade User X's role back to Employee. (c) On the Create/Edit Employee screen, if a user attempts to manually change a Manager's role to Employee while that user still has direct reports, the system shall block the change and display an error: "This user is a reporting manager for active employees and cannot be demoted to Employee." Role is not a manually editable field on the employee form — it is always system-derived |
| EMP-07 | Employee can view and edit their own profile (name, phone — not email, role, department, or manager) |
| EMP-08 | A Manager or Employee shall be able to view the list of their own juniors/subordinates **only if there is at least one**; the subordinate list/menu shall be hidden when they have none |

### 4.3 Department Management

| ID | Requirement |
|----|-------------|
| DEPT-01 | Department profile shall contain: name, code, team overlap limit, status (active/inactive) |
| DEPT-02 | Department shall be a flat list (no hierarchy, no parent/sub-departments) |
| DEPT-03 | Department shall be manageable (CRUD) by HR Admin and Super Admin only |
| DEPT-04 | Managers and Employees shall have read-only visibility into the department list (for use in filters and dropdowns), with no create/edit/delete access |
| DEPT-05 | Department profile shall support soft delete (set inactive, retain data for audit) — a department with active employees assigned to it cannot be hard-deleted |
| DEPT-06 | Deactivating a department shall not reassign or affect its currently linked employees; HR Admin/Super Admin must reassign employees to another department first if required |
| DEPT-07 | Department name and code shall be unique (case-insensitive) among active departments |
| DEPT-08 | Team overlap limit is configured at the department level and applies to all leave requests for employees in that department (REQ-05). There is no per-request override |
| DEPT-09 | All department create/edit/delete/reactivate actions shall be recorded in the Audit Trail (AUD-02) |

### 4.4 Leave Types & Policies

| ID | Requirement |
|----|-------------|
| POL-01 | System shall support configurable leave types |
| POL-02 | Default leave types: Casual Leave, Sick Leave, Earned Leave, Comp-off, Unpaid Leave |
| POL-03 | The Leave Type record shall contain exactly these fields: **Name, Code, Description, AnnualLeaveDays, RequiresAttachment, RequiresHRFlag, IsActive** |
| POL-04 | Default leave type configuration: |

| Leave Type | AnnualLeaveDays | RequiresAttachment | RequiresHRFlag |
|------------|-----------------|--------------------|----------------|
| Casual Leave | 12 | No | No |
| Sick Leave | 6 | **Yes** | **Yes** |
| Earned Leave | 1 | No | No |
| Comp-off | 0 (credited only via approved Comp-Off Requests, COMP-01…) | No | No |
| Unpaid Leave | 0 (no balance tracking) | No | No |

| ID | Requirement |
|----|-------------|
| POL-05 | Every employee shall be assigned leave entitlements **directly from the Leave Type setup**: each employee receives the leave type's AnnualLeaveDays. There is no separate policy object and no different rule per individual employee, department, or grade |
| POL-06 | Leave year shall be January 1 to December 31. There is no carry-forward: any unused balance lapses at year end and the lapse is logged in the audit trail |
| POL-07 | Leave types shall be manageable (CRUD) by HR Admin and Super Admin |

### 4.5 Leave Balance Management

| ID | Requirement |
|----|-------------|
| BAL-01 | System shall maintain per-employee, per-leave-type balance records |
| BAL-02 | Balance shall update in real-time: decreased on approval, restored on cancellation/revocation. A full-day leave deducts 1.0 day; a **half-day leave deducts 0.5 days** from the selected leave type balance |
| BAL-03 | Annual entitlement shall be credited as a lump sum on January 1. For mid-year joiners, entitlement shall be prorated based on the **pending (remaining) calendar days in the year** from the date of joining: `entitled = AnnualLeaveDays × remaining_days_in_year ÷ 365`, rounded to the nearest 0.5 day. **Minimum prorated entitlement is 0.5 days** for any joiner who has at least 1 remaining calendar day in the year (i.e., result never rounds down to 0 unless the joiner joins on Dec 31 with 0 remaining days) |
| BAL-04 | Comp-off balance shall credit only when a Comp-Off Request (COMP section) is approved by the employee's reporting manager. If the employee has no reporting manager linked, the approval request shall route to HR Admin (same no-manager routing rule as leave requests, EMP-03). **LeaveBalance is the single source of truth for comp-off available balance.** On approval, the comp-off LeaveBalance.balance is incremented; on expiry (BAL-06), it is decremented. The CompOffCredit table stores individual credit entries for tracking earn date and expiry date, but the balance displayed to users and used for leave validation always reads from LeaveBalance |
| BAL-05 | Comp-off shall expire **30 days from earn date (fixed, not configurable)** |
| BAL-06 | Daily Hangfire job shall check and expire comp-off balances past expiry date |
| BAL-07 | An employee **cannot submit a leave request if the balance for the selected leave type is zero or would go negative** — the request shall be blocked at submission (REQ-03). Negative balances cannot occur in the system. **Exception: Unpaid Leave is exempt from this check** — an employee may always submit an Unpaid Leave request regardless of balance (Unpaid Leave has no balance tracking). Unpaid Leave still requires L1 and, if applicable, L2 approval |
| BAL-08 | Year-end Hangfire job shall run on Dec 31: lapse all unused balances (no carry-forward), log lapses in audit trail |
| BAL-09 | Leave balances shall be read directly from the database on each request |

### 4.6 Leave Request & Workflow

| ID | Requirement |
|----|-------------|
| REQ-01 | Employee shall be able to apply for leave by selecting: leave type (dropdown), start date, end date, half-day flag (first half / second half), reason (text), attachment (file upload — mandatory when the leave type has RequiresAttachment = Yes, e.g. Sick Leave) |
| REQ-02 | System shall save draft requests (not yet submitted) |
| REQ-03 | On submit, system shall validate: **sufficient balance (request is blocked if balance is zero or would go negative, BAL-07)**, no overlapping approved leave, team overlap limit not exceeded, start date and end date do not fall on a weekend or public holiday (REQ-13) |
| REQ-04 | **Sandwich rule (mandatory, not configurable, applies to all leave types):** **Algorithm:** For a single leave request with start_date and end_date, iterate every day in the range. A day in the range counts as a leave day if and only if it is an employee-selected leave day, OR it is a non-working day (weekend/public holiday) that is connected — through a contiguous chain of non-working days — to at least one employee-selected leave day on **each side** (left and right) within the range. An isolated non-working day that has employee-selected leave days on both sides but is NOT part of a chain touching those leave days does not count. Non-working days at the edges of the range (before the first leave day or after the last leave day) are always included if they form a chain with the adjacent leave day. A non-working day bridging **two separate leave requests** is never counted. **Examples where sandwich rule APPLIES:** (1) Sunday (Weekend) + Monday (Leave) + Tuesday (Public Holiday) = **3 leave days** — Monday leave has Sunday non-working on its left and Tuesday non-working on its right, both chained directly, so all three count. (2) Thursday (Holiday) + Friday (Leave) + Saturday (Weekend) + Sunday (Weekend) = **4 leave days** — Friday leave chains left to Thursday and right to the weekend block; all four count. (3) Monday (Holiday) + Tuesday (Leave) + Wednesday (Holiday) = **3 leave days** — Tuesday leave chains to Monday holiday on left and Wednesday holiday on right; all three count. **Examples where sandwich rule does NOT apply:** (1) Monday (Leave) + Tuesday (Holiday) + Wednesday (Leave) = **2 leave days** — Tuesday holiday sits between two leave days but is not chained to a non-working day on either side; it is an isolated non-working day between two working leave days and does NOT count. Only Monday and Wednesday are counted. (2) Friday (Leave – Request 1) + Saturday & Sunday (Weekend) + Monday (Leave – Request 2) = **2 leave days** — weekend is between two separate requests and is never counted. (3) Monday (Leave) + Tuesday (Holiday) + Wednesday (Working Day) = **1 leave day** — Tuesday holiday has a working day on its right so the chain is broken; only Monday counts. |
| REQ-05 | Team overlap limit: configurable per department (default max 2 employees from same department on leave on same date). Any approved leave — full-day or half-day — on a given date counts as **1 unit** toward the team overlap limit for that date. Concurrent submissions: first-commit-wins at database level |
| REQ-06 | An employee cannot submit two separate half-day leave requests for the same date. If an employee submits a half-day (AM or PM) leave for a date on which they already have an approved or pending half-day leave for the **other** period, the system shall block the second submission and prompt the employee to instead modify the existing request to a full-day leave. A full-day leave on that date deducts 1.0 day (0.5 + 0.5) |
| REQ-07 | Leave request status lifecycle: Draft → Submitted → Pending L1 Approval → L1 Approved → Pending L2 Approval (conditional) → Approved → Active (on start date) → Completed (on end date) |
| REQ-08 | Alternative status paths: → Rejected (at any approval level, mandatory reason) → Cancelled (by employee **before start date only**; once the leave start date is reached the employee cannot cancel) → Revoked (by HR Admin **before start date only**, mandatory reason; HR Admin cannot revoke a leave after it has started) |
| REQ-09 | Retroactive leave requests (start date in the past) shall always require HR Admin (L2) approval regardless of leave type |
| REQ-10 | Cancelled leave shall restore balance immediately |
| REQ-11 | HR Admin can revoke a leave request **only before the leave start date**. On revocation, the full leave balance is restored (since no leave days have been consumed yet). Revocation after start date is not permitted |
| REQ-12 | Attachment upload: PDF, JPG, PNG only, max 5MB, stored in local filesystem (S3 in Phase 2) |
| REQ-13 | Employees **cannot apply leave on a public holiday or weekend (Saturday/Sunday)** — leave start and end dates must be working days. (Weekends/holidays inside the range still count via the sandwich rule, REQ-04) |
| REQ-14 | On submission, an email shall be sent to the employee's reporting manager; if no reporting manager is linked, the email shall be sent to HR Admin |

### 4.7 Comp-Off Requests (new)

| ID | Requirement |
|----|-------------|
| COMP-01 | Employee shall be able to submit a Comp-Off Request via a dedicated screen with fields: **Date, Description, IsHalfDay (checkbox), Start Time, End Time** |
| COMP-02 | The requested date must be a public holiday or a weekend (Saturday/Sunday) — comp-off is earned only by working on a holiday/weekend |
| COMP-03 | Worked hours are computed from Start Time and End Time. A **half-day** comp-off request is allowed only if worked hours are **more than 4 hours**; a **full-day** request is allowed only if worked hours are **8 hours or more**. Requests below 4 hours shall be blocked |
| COMP-04 | Comp-Off Requests shall be approved or rejected by the employee's reporting manager (or HR Admin if no manager is linked). Rejection requires a mandatory reason. **A submitted Comp-Off Request cannot be cancelled by the employee** — the employee must wait for the approver to reject it if it was submitted in error |
| COMP-05 | On approval, comp-off balance shall be credited: 0.5 day for half-day, 1 day for full-day (BAL-04), with expiry per BAL-05 |
| COMP-06 | Email notifications: submission → to manager (or HR Admin if none); approval/rejection → to employee |
| COMP-07 | Comp-off request submission, approval, and rejection shall be recorded in the Audit Trail (AUD-02) |

### 4.8 Approval Engine

| ID | Requirement |
|----|-------------|
| APR-01 | L1 approval: required for all submitted leave requests. The L1 approver is the employee's reporting manager; if no manager is linked, HR Admin acts as L1 approver. **When HR Admin acts as L1, L2 is automatically skipped** — HR Admin's single approval is the final approval, regardless of leave duration, HR flag, or retroactive status (APR-02 conditions do not apply in this scenario) |
| APR-02 | L2 approval (HR Admin): required when **any** of the following conditions are true: leave duration > 3 consecutive days, OR the selected leave type has **RequiresHRFlag = Yes** (e.g. Sick Leave, POL-04), OR retroactive request (REQ-09). **L2 approval can occur only after L1 (Manager) approval is completed** — a request never reaches HR Admin before the manager has approved it |
| APR-03 | Escalation: if a pending approval is not acted on, the system shall **automatically send an escalation/reminder email every 2 days** to the pending approver until the request is approved or rejected |
| APR-04 | Escalation Hangfire job shall run daily, checking all Pending L1 and Pending L2 requests (and pending Comp-Off Requests) |
| APR-05 | Approval/rejection shall trigger immediate email notification to the employee |

### 4.9 Public Holiday Calendar

| ID | Requirement |
|----|-------------|
| HOL-01 | HR Admin shall manage public holidays: date, name |
| HOL-02 | Public holidays shall be factored into: sandwich rule calculation (REQ-04), working day counting, team overlap detection, comp-off eligibility (COMP-02) |
| HOL-03 | Holiday list shall support bulk import via CSV upload |
| HOL-04 | Employees **cannot take leave on a public holiday or weekend** — the Apply for Leave calendar shall disable holidays and weekends as start/end dates (REQ-13) |

### 4.10 Notifications

| ID | Requirement |
|----|-------------|
| NOT-01 | Email notifications via SendGrid for: leave applied (to manager, or HR Admin if no manager), approved (to employee), rejected (to employee + reason), cancelled (to manager if exists, otherwise to HR Admin), revoked (to employee + reason), reminder/escalation to approve (to pending approver, every 2 days per APR-03), comp-off request applied (to manager, or HR Admin if no manager), comp-off approved/rejected (to employee) |
| NOT-02 | In-app notification center: list of all notifications with read/unread status, click to navigate to relevant leave or comp-off request |
| NOT-03 | Google Calendar integration: on leave approval (any leave type), create all-day event in employee's Google Calendar. On cancel/revoke, delete the event. This applies to all leave types including Unpaid Leave and Comp-off |
| NOT-04 | Calendar sync shall run as Hangfire background job with retry (3 attempts, exponential backoff) |
| NOT-05 | If calendar sync fails after all retries, log error and mark notification as "calendar sync failed" (do not block the leave approval) |
| NOT-06 | If SendGrid is unavailable, emails shall be queued in Hangfire with retry (5 attempts over 24 hours). After all retries fail, mark as "email delivery failed" in notification log |

### 4.11 Reporting & Dashboard

| ID | Requirement |
|----|-------------|
| RPT-01 | Employee dashboard (Employee and Manager roles only): leave balance cards (one per type showing used/total), leave history table with status filter, upcoming public holidays list |
| RPT-02 | Manager dashboard (Manager role): team calendar (month view showing which direct reports are on leave each day), pending approval count with badge (leave + comp-off), team balance summary table. Visible only when the manager has at least one subordinate (EMP-08) |
| RPT-03 | HR dashboard: department-wise utilization bar chart, monthly leave trend line chart, policy compliance report (employees who exceeded limits), CSV export on all reports |
| RPT-04 | Super Admin dashboard: system-wide metrics (total leaves today, pending approvals count, policy violations), full audit log viewer with search, filter by user/action/date |
| RPT-05 | All reports shall support date range filtering. Report export format: CSV (Phase 1) |

### 4.12 Audit Trail

| ID | Requirement |
|----|-------------|
| AUD-01 | System shall log every state-changing action: who performed it, what changed (old value → new value), when, from which IP address |
| AUD-02 | Auditable actions: leave request CRUD, approval/rejection, cancellation, revocation, comp-off request CRUD and approval/rejection, leave type/policy changes, holiday changes, role changes, employee profile changes, department CRUD, account lock/unlock, login/logout |
| AUD-03 | Audit log shall be append-only — no edits or deletes allowed |
| AUD-04 | Audit log shall be viewable by: **Super Admin (all entries) and HR Admin (all entries)** |
| AUD-05 | Audit log shall be searchable by: user, action type, date range, entity type |
| AUD-06 | Audit log shall be retained for minimum 3 years |

### 4.13 Initial Data Seeding

| ID | Requirement |
|----|-------------|
| SEED-01 | On initial deployment, system shall seed exactly two default user accounts: one with role Super Admin, one with role HR Admin |
| SEED-02 | Both seeded accounts shall use password `Admin@123` (meets AUTH-06 policy: 8+ characters, 1 uppercase, 1 number) |
| SEED-03 | Both seeded accounts shall be created with status Active and shall use local login (not tied to Azure AD) |
| SEED-04 | System shall seed one default Department named "HR" to satisfy the mandatory department field (EMP-01); both seeded accounts shall be mapped to this HR department |
| SEED-05 | System shall seed the default Leave Types defined in POL-02: Casual Leave, Sick Leave, Earned Leave, Comp-off, Unpaid Leave — with the default field values in POL-04 |
| SEED-06 | Seeding shall run once via a migration/seed script (idempotent — re-running shall not create duplicate users, leave types, or departments) |

---

## 5. Non-Functional Requirements

| ID | Requirement |
|----|-------------|
| NFR-01 | API response time: p50 < 200ms, p99 < 500ms for CRUD endpoints |
| NFR-02 | Report generation: p99 < 2 seconds for department-level reports |
| NFR-03 | System shall support 500 concurrent authenticated users |
| NFR-04 | System uptime target: 99.9% |
| NFR-05 | All API endpoints shall require authentication (JWT or SSO token) |
| NFR-06 | All API endpoints shall enforce role-based access control per Permission Matrix |
| NFR-07 | API rate limiting: 100 requests/minute per authenticated user |
| NFR-08 | All sensitive data (passwords, tokens) shall be encrypted at rest and never logged |
| NFR-09 | All employee PII shall be exportable on request (GDPR Article 15) |
| NFR-10 | Employee data shall be soft-deletable with anonymization option (GDPR Article 17) |
| NFR-11 | System shall operate in IST timezone (Phase 1), multi-timezone support in Phase 2 |
| NFR-12 | Leave year: January 1 to December 31 |

---

## 6. Tech Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| Backend API | C# 12, .NET 8, ASP.NET Core Web API | REST API server |
| Database | PostgreSQL 15+ | Primary data store |
| Job Storage | PostgreSQL (Hangfire storage) | Hangfire job persistence |
| Task Queue | Hangfire | Background jobs: email, calendar sync, escalation, expiry |
| Scheduler | Hangfire Recurring Jobs | Scheduled jobs: daily, yearly |
| Auth | JWT + Azure AD OAuth2 | Authentication and SSO |
| Email | SendGrid API v3 | Transactional email notifications |
| Calendar | Google Calendar API v3 | Leave event sync |
| Migrations | EF Core Migrations | Database schema versioning |
| Testing | xUnit | Unit + integration testing |
| Frontend | React 17 (Functional Components + Hooks) | SPA web application |
| UI Component Library | MUI (Material-UI) v5 | UI components, theming |
| State Management | Redux Toolkit (Store + Redux-Saga) | Centralized application state |
| HTTP Client | Axios + Interceptors | API communication, JWT attachment |
| Routing | React Router with Protected Routes | RBAC-based navigation |
| Charts | Chart.js via react-chartjs-2 | Dashboard visualizations |
| Calendar | FullCalendar (React wrapper) | Team leave calendar view |

---

## 7. Third-Party Integrations

### 7.1 Azure AD (SSO)

| Item | Detail |
|------|--------|
| Protocol | OAuth2 Authorization Code Flow |
| Scope | openid, profile, email, User.Read, GroupMember.Read.All |
| Token exchange | Authorization code → access_token + id_token |
| Role mapping | Azure AD group membership → LMS role (mapping maintained in application configuration; no admin UI in Phase 1) |
| Fallback | If Azure AD is unreachable, local login remains available |

### 7.2 SendGrid (Email)

| Item | Detail |
|------|--------|
| API version | v3 |
| Use case | Transactional emails: leave/comp-off status changes, 2-day escalation reminders |
| Templates | SendGrid dynamic templates (template IDs stored in config) |
| Failure handling | Queue in Hangfire, retry 5x with exponential backoff over 24h |

### 7.3 Google Calendar (Event Sync)

| Item | Detail |
|------|--------|
| API version | v3 |
| Auth | OAuth2 (user grants calendar access on first use) |
| Events | Create all-day event on leave approval, delete on cancel/revoke |
| Failure handling | Hangfire retry 3x, log failure, do not block leave approval |

---

## 8. Data Model Overview

### Core Entities

| Entity | Key Fields | Relationships |
|--------|-----------|---------------|
| User | id, email, name, phone (nullable), department_id, designation, date_of_joining, reporting_manager_id (nullable), role, status | belongs_to Department, has_many LeaveRequests, has_many LeaveBalances, has_many CompOffRequests |
| Department | id, name, code, team_overlap_limit, status | has_many Users |
| LeaveType | id, name, code, description, annual_leave_days, requires_attachment, requires_hr_flag, is_active | has_many LeaveBalances, has_many LeaveRequests |
| LeaveBalance | id, user_id, leave_type_id, year, total_entitled, used, balance | belongs_to User, belongs_to LeaveType |
| LeaveRequest | id, user_id, leave_type_id, start_date, end_date, half_day, half_day_period, days_count, reason, attachment_path, status, applied_at, approved_at, cancelled_at, revoked_at | belongs_to User, belongs_to LeaveType, has_many ApprovalSteps |
| ApprovalStep | id, leave_request_id, approver_id, level (L1/L2), status, acted_at, comments | belongs_to LeaveRequest, belongs_to User (approver) |
| CompOffRequest | id, user_id, date, description, is_half_day, start_time, end_time, worked_hours, status (pending/approved/rejected), approver_id, acted_at, rejection_reason | belongs_to User, belongs_to User (approver) |
| CompOffCredit | id, user_id, comp_off_request_id, earned_date, expiry_date, days (0.5 or 1), status (active/used/expired) | belongs_to User, belongs_to CompOffRequest |
| Holiday | id, date, name, year | — |
| Notification | id, user_id, type, title, message, read, related_entity_type, related_entity_id, email_status, calendar_status, created_at | belongs_to User |
| AuditLog | id, user_id, action, entity_type, entity_id, old_value (JSON), new_value (JSON), ip_address, created_at | belongs_to User |

---

## 9. API Endpoints Overview

### Authentication
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | /api/v1/auth/sso/login | No | Redirect to Azure AD |
| GET | /api/v1/auth/sso/callback | No | Handle Azure AD callback, issue JWT |
| POST | /api/v1/auth/login | No | Local email+password login |
| POST | /api/v1/auth/refresh | Yes (refresh) | Refresh access token |
| POST | /api/v1/auth/logout | Yes | Invalidate refresh token |

### Employees
| Method | Path | Auth | Roles | Description |
|--------|------|------|-------|-------------|
| GET | /api/v1/employees | Yes | HR, Super Admin | List employees (filterable) |
| GET | /api/v1/employees/me | Yes | All | Own profile |
| GET | /api/v1/employees/team | Yes | Manager (only if subordinates exist) | List own direct reports only — scoped to employees whose reporting_manager_id = current user |
| GET | /api/v1/employees/{id} | Yes | Manager (own team), HR, Super Admin | Employee detail |
| POST | /api/v1/employees | Yes | HR, Super Admin | Create employee |
| PUT | /api/v1/employees/{id} | Yes | HR, Super Admin | Update employee |
| DELETE | /api/v1/employees/{id} | Yes | HR, Super Admin | Soft delete employee |

### Departments
| Method | Path | Auth | Roles | Description |
|--------|------|------|-------|-------------|
| GET | /api/v1/departments | Yes | All (read-only for Employee, Manager) | List departments (for dropdowns/filters) |
| GET | /api/v1/departments/{id} | Yes | All (read-only for Employee, Manager) | Department detail |
| POST | /api/v1/departments | Yes | HR, Super Admin | Create department |
| PUT | /api/v1/departments/{id} | Yes | HR, Super Admin | Update department |
| DELETE | /api/v1/departments/{id} | Yes | HR, Super Admin | Soft delete department |

### Leave Types & Policies
| Method | Path | Auth | Roles | Description |
|--------|------|------|-------|-------------|
| GET | /api/v1/leave-types | Yes | All | List leave types |
| POST | /api/v1/leave-types | Yes | HR, Super Admin | Create leave type |
| PUT | /api/v1/leave-types/{id} | Yes | HR, Super Admin | Update leave type |
| DELETE | /api/v1/leave-types/{id} | Yes | HR, Super Admin | Deactivate leave type |

### Leave Balances
| Method | Path | Auth | Roles | Description |
|--------|------|------|-------|-------------|
| GET | /api/v1/balances/me | Yes | Employee, Manager | Own balances (all types) |
| GET | /api/v1/balances/team | Yes | Manager (only if subordinates exist) | Direct reports' balances |
| GET | /api/v1/balances/department/{id} | Yes | HR, Super Admin | Department balances |

### Leave Requests
| Method | Path | Auth | Roles | Description |
|--------|------|------|-------|-------------|
| POST | /api/v1/leave-requests | Yes | Employee, Manager | Apply for leave |
| GET | /api/v1/leave-requests | Yes | Employee, Manager | Own requests (filterable by status, date) |
| GET | /api/v1/leave-requests/{id} | Yes | Owner, Manager, HR, Super Admin | Request detail |
| PUT | /api/v1/leave-requests/{id} | Yes | Owner (draft only) | Update draft request |
| DELETE | /api/v1/leave-requests/{id} | Yes | Owner (draft only) | Delete draft |
| POST | /api/v1/leave-requests/{id}/submit | Yes | Owner | Submit draft for approval (emails manager / HR Admin per REQ-14) |
| POST | /api/v1/leave-requests/{id}/cancel | Yes | Owner (before start date) | Cancel submitted/approved request |
| POST | /api/v1/leave-requests/{id}/revoke | Yes | HR, Super Admin | Revoke active leave |

### Comp-Off Requests
| Method | Path | Auth | Roles | Description |
|--------|------|------|-------|-------------|
| POST | /api/v1/comp-off-requests | Yes | Employee, Manager | Submit comp-off request |
| GET | /api/v1/comp-off-requests | Yes | Employee, Manager | Own comp-off requests |
| GET | /api/v1/comp-off-requests/pending | Yes | Manager, HR, Super Admin | Pending comp-off approvals |
| POST | /api/v1/comp-off-requests/{id}/approve | Yes | Manager (or HR if no manager) | Approve — credits comp-off balance |
| POST | /api/v1/comp-off-requests/{id}/reject | Yes | Manager (or HR if no manager) | Reject (reason required) |

### Approvals
| Method | Path | Auth | Roles | Description |
|--------|------|------|-------|-------------|
| GET | /api/v1/approvals/pending | Yes | Manager, HR, Super Admin | List pending approvals. **Manager scope:** only requests where the employee's reporting_manager_id = current Manager's user_id. Manager cannot view or action approvals for employees outside their direct reports |
| POST | /api/v1/approvals/{request_id}/approve | Yes | Manager (L1), HR (L2 — only after L1 approved) | Approve request |
| POST | /api/v1/approvals/{request_id}/reject | Yes | Manager (L1), HR (L2) | Reject request (reason required) |

### Holidays
| Method | Path | Auth | Roles | Description |
|--------|------|------|-------|-------------|
| GET | /api/v1/holidays | Yes | All | List holidays (filterable by year) |
| POST | /api/v1/holidays | Yes | HR, Super Admin | Create holiday |
| PUT | /api/v1/holidays/{id} | Yes | HR, Super Admin | Update holiday |
| DELETE | /api/v1/holidays/{id} | Yes | HR, Super Admin | Delete holiday |
| POST | /api/v1/holidays/bulk-import | Yes | HR, Super Admin | CSV bulk import |

### Locked Accounts
| Method | Path | Auth | Roles | Description |
|--------|------|------|-------|-------------|
| GET | /api/v1/accounts/locked | Yes | HR, Super Admin | List locked accounts |
| POST | /api/v1/accounts/{id}/unlock | Yes | HR, Super Admin | Unlock account |

### Reports
| Method | Path | Auth | Roles | Description |
|--------|------|------|-------|-------------|
| GET | /api/v1/reports/team-calendar | Yes | Manager, HR, Super Admin | Team calendar view |
| GET | /api/v1/reports/utilization | Yes | HR, Super Admin | Department utilization |
| GET | /api/v1/reports/trends | Yes | HR, Super Admin | Leave trends |
| GET | /api/v1/reports/compliance | Yes | HR, Super Admin | Policy compliance |
| GET | /api/v1/reports/export | Yes | HR, Super Admin | CSV export |

### Notifications
| Method | Path | Auth | Roles | Description |
|--------|------|------|-------|-------------|
| GET | /api/v1/notifications | Yes | All | Own notifications |
| PUT | /api/v1/notifications/{id}/read | Yes | All | Mark as read |
| PUT | /api/v1/notifications/read-all | Yes | All | Mark all as read |

### Audit Log
| Method | Path | Auth | Roles | Description |
|--------|------|------|-------|-------------|
| GET | /api/v1/audit-log | Yes | HR (all), Super Admin (all) | Search audit log |

### System
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | /health | No | Health check (DB, Hangfire) |

---

## 10. Frontend — React Application (Phase 1)

### 10.1 Technology & Architecture

| Item | Detail |
|------|--------|
| Framework | React 17 with Functional Components + Hooks |
| UI Library | MUI (Material-UI) v5 |
| State Management | Redux Toolkit (Store + Redux-Saga) — auth state, leave balances, notifications |
| Auth Integration | MSAL React for Azure AD SSO; JWT stored in memory (access) + HttpOnly cookie (refresh) |
| API Communication | Axios with JWT interceptor; automatic token refresh on 401 |
| Route Protection | `ProtectedRoute` (login check) + `RoleProtectedRoute` (role-based screen access) |
| Build & Deploy | Vite, Nginx-served |
| Styling | MUI theming + SCSS, responsive layout (desktop-first, min 1024px) |

---

### 10.2 Application Modules

| Module | Description |
|--------|-------------|
| `AuthModule` | Login, token handling |
| `EmployeeModule` | Profile management, subordinate list |
| `LeaveModule` | Apply, view, manage leave requests |
| `CompOffModule` | Submit and track comp-off requests |
| `ApprovalModule` | Approve/reject leave and comp-off (Manager, HR) |
| `HolidayModule` | Holiday calendar view and management |
| `AdminModule` | Leave types, employee/department management, locked accounts (HR, Super Admin) |
| `DashboardModule` | Role-specific dashboard screens |
| `ReportsModule` | Charts, tables, CSV export (HR, Super Admin) |
| `AuditModule` | Audit log viewer (HR, Super Admin) |
| `NotificationsModule` | In-app notification center (all roles) |
| `SharedModule` | Reusable components |

---

### 10.3 Screens — Role Visibility Matrix

> **Legend:** ✅ Full Access | 👁 Read Only | ❌ No Access

| Screen | Employee | Manager | HR Admin | Super Admin |
|--------|----------|---------|----------|-------------|
| Login | ✅ | ✅ | ✅ | ✅ |
| Employee Dashboard | ✅ | ✅ | ❌ | ❌ |
| My Profile | ✅ | ✅ | ❌ | ❌ |
| Apply for Leave | ✅ | ✅ | ❌ | ❌ |
| My Leave History | ✅ | ✅ | ❌ | ❌ |
| My Leave Balances | ✅ | ✅ | ❌ | ❌ |
| Cancel Leave Request | ✅ | ✅ | ❌ | ❌ |
| Comp-Off Request | ✅ | ✅ | ❌ | ❌ |
| Holiday Calendar (view) | ✅ | ✅ | ✅ | ✅ |
| Notification Center | ✅ | ✅ | ✅ | ✅ |
| Manager Dashboard | ❌ | ✅ | ❌ | ❌ |
| Team Calendar | ❌ | ✅ | ✅ | ✅ |
| Subordinate List | ❌ | ✅ (only if subordinates exist) | ✅ | ✅ |
| Pending Approvals List (leave + comp-off) | ❌ | ✅ (L1) | ✅ (L2) | ✅ |
| Approve / Reject Leave & Comp-Off | ❌ | ✅ | ✅ | ✅ |
| Team Leave Balance Summary | ❌ | ✅ | ✅ | ✅ |
| HR Dashboard | ❌ | ❌ | ✅ | ✅ |
| Employee List & Management | ❌ | 👁 (own team) | ✅ | ✅ |
| Create / Edit Employee | ❌ | ❌ | ✅ | ✅ |
| Department List | 👁 | 👁 | ✅ | ✅ |
| Create / Edit / Deactivate Department | ❌ | ❌ | ✅ | ✅ |
| Leave Type Management | ❌ | ❌ | ✅ | ✅ |
| Holiday Management (CRUD) | ❌ | ❌ | ✅ | ✅ |
| Holiday Bulk Import (CSV) | ❌ | ❌ | ✅ | ✅ |
| Revoke Active Leave | ❌ | ❌ | ✅ | ✅ |
| Department Reports | ❌ | ❌ | ✅ | ✅ |
| CSV Export | ❌ | ❌ | ✅ | ✅ |
| Audit Log Viewer | ❌ | ❌ | ✅ (all entries) | ✅ (all entries) |
| Super Admin Dashboard | ❌ | ❌ | ❌ | ✅ |
| Locked Account Management | ❌ | ❌ | ✅ | ✅ |

---

### 10.4 Screen Specifications

#### SCR-01 — Login
**Roles:** All
**Description:** Entry point for unauthenticated users.
- "Sign in with Microsoft" button (Azure AD SSO)
- Local login form (email + password) as fallback
- Displays account-locked message (after 3 failed attempts, AUTH-07) with instructions on contacting HR/Admin
- Redirects to role-appropriate dashboard on successful login: Employee → /me/dashboard (SCR-02), Manager → /me/dashboard (SCR-02) with Manager Dashboard (SCR-11) accessible via sidebar, HR Admin → /hr/dashboard (SCR-14), Super Admin → /admin/dashboard (SCR-21)

---

#### SCR-02 — Employee Dashboard
**Roles:** Employee, Manager (default landing screen post-login for these roles)
**Description:** Personal leave summary and quick actions.
- Leave balance cards: one card per active leave type showing `Used / Total` with a progress bar
- Quick action buttons: "Apply for Leave", "Request Comp-Off"
- Recent leave requests table (last 5): type, dates, status badge
- Upcoming public holidays widget (next 30 days)
- Unread notification badge in top navbar

---

#### SCR-03 — My Profile
**Roles:** Employee, Manager
**Description:** View and partially edit own profile.
- Displays: Name, Email, Department, Designation, Date of Joining, Reporting Manager
- Editable fields: Name, Phone
- Non-editable (display only): Email, Role, Department, Reporting Manager
- Shows current role badge

---

#### SCR-04 — Apply for Leave
**Roles:** Employee, Manager
**Description:** Leave application form with real-time validation.
- Leave type dropdown (active leave types)
- Date range picker (start date, end date) — calendar highlights public holidays; **weekends and public holidays are disabled as start/end dates** (REQ-13)
- Half-day toggle (AM / PM) — enabled only when start = end date
- Reason text area (required)
- File attachment upload (PDF/JPG/PNG, max 5MB) — mandatory when leave type requires attachment (e.g. Sick Leave)
- Real-time balance check: shows available balance for selected leave type; **Submit is blocked (disabled with an error message) if balance is zero or the requested days would make it negative** (BAL-07)
- Sandwich rule note: computed leave day count including sandwiched weekends/holidays (REQ-04) is shown before submit
- Validation feedback: insufficient balance, overlap conflict, team overlap limit
- Save as Draft / Submit buttons
- Retroactive request banner (if start date is in the past): warns HR approval is required

---

#### SCR-05 — My Leave History
**Roles:** Employee, Manager
**Description:** Paginated list of own leave requests.
- Filter bar: status (All / Draft / Pending / Approved / Rejected / Cancelled / Revoked), leave type, date range
- Table columns: Leave Type, Start–End Date, Days, Status, Applied On, Actions
- Actions per row: View Detail | Cancel (if cancellable) | Delete Draft
- Status badges with colour coding

---

#### SCR-06 — Leave Request Detail
**Roles:** Owner, Manager (own team), HR Admin, Super Admin
**Description:** Full detail view of a single leave request.
- All leave request fields displayed
- Approval timeline: L1 (Manager) → L2 (HR Admin if applicable), each step showing approver name, action, timestamp, comments
- Cancel button (employee, before start date)
- Revoke button (HR Admin / Super Admin, active leave)
- Rejection reason displayed if rejected

---

#### SCR-07 — My Leave Balances
**Roles:** Employee, Manager
**Description:** Detailed breakdown of all leave balances for the current year.
- Table: Leave Type | Annual Entitlement | Used | Available
- Comp-off section: list of comp-off credits with earn date, expiry date, days, status

---

#### SCR-08 — Holiday Calendar
**Roles:** All
**Description:** Full-year holiday calendar view.
- Monthly calendar view with public holidays marked
- List view toggle showing holiday name and date
- Weekends and holidays visually indicated as non-working days (leave cannot be applied on them)

---

#### SCR-09 — Comp-Off Request (new)
**Roles:** Employee, Manager
**Description:** Submit and track comp-off requests for work done on a holiday/weekend.
- Form fields: Date (must be a weekend or public holiday), Description, **IsHalfDay checkbox**, Start Time, End Time
- Worked hours auto-computed from start/end time
- Validation: half-day allowed only if worked hours > 4; full-day allowed only if worked hours ≥ 8; below 4 hours the request is blocked
- Submit sends the request to the reporting manager (or HR Admin if no manager linked) with email notification
- "My Comp-Off Requests" table below the form: Date, Hours, Half/Full, Status, Approver, Actions (View)

---

#### SCR-10 — Notification Center
**Roles:** All
**Description:** In-app notification inbox.
- List of notifications: icon (type), title, message snippet, timestamp, read/unread indicator
- Click notification → navigate to related leave or comp-off request
- Mark as Read | Mark All as Read buttons
- Unread count badge in top navigation

---

#### SCR-11 — Manager Dashboard
**Roles:** Manager
**Description:** Team leave overview for approvers. Visible only when the manager has at least one subordinate (EMP-08).
- Pending approvals count card (leave + comp-off) with "View All" link
- Team calendar (month view): shows which direct reports are on leave each day (colour by leave type)
- Team balance summary table: Employee | Leave Type | Available Balance
- Quick Approve shortcut for oldest pending requests

---

#### SCR-12 — Pending Approvals List
**Roles:** Manager (L1), HR Admin (L2), Super Admin
**Description:** Leave and comp-off requests awaiting action.
- Tabs: Leave Requests | Comp-Off Requests
- Filter: level (L1 / L2), leave type, employee, date
- Table columns: Employee, Type, Dates, Days/Hours, Applied On, Days Pending, Actions
- Actions per row: View | Approve | Reject (individual actions only — no bulk actions)
- HR Admin (L2) sees a leave request only after L1 (Manager) approval is completed (APR-02)
- Rejection modal: mandatory reason text

---

#### SCR-13 — Team Calendar
**Roles:** Manager, HR Admin, Super Admin
**Description:** Calendar showing team leave at a glance.
- Month / Week view toggle (FullCalendar)
- Each leave event shows employee name and leave type
- Click event → Leave Request Detail (SCR-06)
- Filter by department (HR Admin / Super Admin only)

---

#### SCR-14 — HR Dashboard
**Roles:** HR Admin, Super Admin
**Description:** Department-wide leave analytics.
- Department utilization bar chart (leave days used vs. entitled, per department)
- Monthly leave trend line chart (current year)
- Policy compliance alert list: retroactive requests, sandwich-rule adjusted requests, blocked submission attempts, etc.
- Top cards: Total Leaves Today | Pending L2 Approvals | Policy Violations This Month

---

#### SCR-15 — Employee List & Management
**Roles:** HR Admin, Super Admin (full CRUD) | Manager (read-only, own team, only if subordinates exist)
**Description:** Organisation-wide employee directory.
- Searchable, filterable table: Name, Email, Department, Designation, Role, Status
- Filter by: department, role, status
- Actions: View Profile | Edit | Deactivate (HR Admin / Super Admin only)
- "Add Employee" button (HR Admin / Super Admin only)

---

#### SCR-16 — Create / Edit Employee
**Roles:** HR Admin, Super Admin
**Description:** Form to onboard or update an employee profile.
- Fields: Name, Email, **Department (dropdown of active departments)**, Designation, Date of Joining, **Reporting Manager (dropdown of active employees; optional — if left empty, leave notifications route to HR Admin per EMP-03)**, Status
- Role is derived automatically (not a form field)
- Save / Cancel buttons

---

#### SCR-16a — Department List & Management
**Roles:** HR Admin, Super Admin (full CRUD) | Employee, Manager (read-only)
**Description:** Organisation-wide department directory.
- Searchable, filterable table: Name, Code, Team Overlap Limit, Status, Employee Count
- Filter by: status
- Actions: View | Edit | Deactivate (HR Admin / Super Admin only) — Employee and Manager see the table read-only with no action buttons
- "Add Department" button (HR Admin / Super Admin only)

---

#### SCR-16b — Create / Edit Department
**Roles:** HR Admin, Super Admin
**Description:** Form to create or update a department.
- Fields: Name, Code, Team Overlap Limit, Status
- Validation: Name and Code must be unique among active departments (DEPT-07)
- Deactivating a department with active employees shows a warning; does not block save but flags for HR follow-up (DEPT-06)
- Save / Cancel buttons

---

#### SCR-17 — Leave Type Management
**Roles:** HR Admin, Super Admin
**Description:** CRUD interface for leave types.
- List of leave types with active/inactive toggle
- Add / Edit leave type form fields (matching POL-03 exactly):
  - Name
  - Code
  - Description
  - Annual Leave Days
  - Requires Attachment (toggle)
  - Requires HR Flag (toggle)
  - Is Active (toggle)

---

#### SCR-18 — Holiday Management
**Roles:** HR Admin, Super Admin
**Description:** Manage the public holiday calendar.
- Year selector
- Table of holidays: Date, Name
- Add / Edit / Delete holiday (modal form)
- "Bulk Import CSV" button: file picker → preview → confirm upload

---

#### SCR-19 — Department Reports
**Roles:** HR Admin, Super Admin
**Description:** Reporting suite with export.
- Tabs: Utilization | Trends | Compliance
- Filter bar: Department, Date Range, Leave Type
- Utilization tab: bar chart (used vs. entitled per department)
- Trends tab: line chart (monthly leave volume for the year)
- Compliance tab: table of flagged items (retroactive requests, sandwich-rule adjusted requests, etc.)
- "Export CSV" button on each tab

---

#### SCR-20 — Audit Log Viewer
**Roles:** HR Admin (all entries), Super Admin (all entries)
**Description:** Searchable, append-only audit trail.
- Search bar + filters: User, Action Type, Entity Type, Date Range
- Table: Timestamp, User, Action, Entity Type, Entity ID, IP Address
- Row expand → shows Old Value / New Value JSON diff
- Pagination (50 rows per page)

---

#### SCR-21 — Super Admin Dashboard
**Roles:** Super Admin
**Description:** System-wide metrics.
- Top cards: Total Active Employees | Total Leaves Today (system-wide) | Pending Approvals (all levels) | Policy Violations This Month
- Full audit log access (links to SCR-20)

---

#### SCR-22 — Locked Account Management
**Roles:** HR Admin, Super Admin
**Description:** Manage accounts locked after 3 consecutive failed login attempts (AUTH-07).
- Table: list of locked accounts — Name, Email, Locked At, Failed Attempt Count
- **Unlock** button per row (with confirmation dialog)
- Unlock action recorded in Audit Trail (AUD-02)

---

### 10.5 React Route Structure

```
/auth
  /login                    → SCR-01 (All, unauthenticated)
  /sso/callback             → SSO token handler

/ (protected, ProtectedRoute)
  /holidays                 → SCR-08 Holiday Calendar (All)
  /notifications            → SCR-10 Notification Center (All)

  /me (RoleProtectedRoute: Employee, Manager)
    /dashboard              → SCR-02 Employee Dashboard
    /profile                → SCR-03 My Profile

  /leave (RoleProtectedRoute: Employee, Manager)
    /apply                  → SCR-04 Apply for Leave
    /history                → SCR-05 My Leave History
    /balances               → SCR-07 My Leave Balances
    /comp-off               → SCR-09 Comp-Off Request
    /:id                    → SCR-06 Leave Request Detail (all with access)

  /manager (RoleProtectedRoute: Manager)
    /dashboard              → SCR-11 Manager Dashboard
    /team                   → Subordinate list (only if subordinates exist)

  /approvals (RoleProtectedRoute: Manager, HR Admin, Super Admin)
    /                       → SCR-12 Pending Approvals List (leave + comp-off)
    /team-calendar          → SCR-13 Team Calendar

  /hr (RoleProtectedRoute: HR Admin+)
    /dashboard              → SCR-14 HR Dashboard
    /employees              → SCR-15 Employee List
    /employees/new          → SCR-16 Create Employee
    /employees/:id/edit     → SCR-16 Edit Employee
    /departments            → SCR-16a Department List
    /departments/new        → SCR-16b Create Department
    /departments/:id/edit   → SCR-16b Edit Department
    /leave-types            → SCR-17 Leave Type Management
    /holidays/manage        → SCR-18 Holiday Management
    /reports                → SCR-19 Department Reports
    /audit-log              → SCR-20 Audit Log Viewer
    /locked-accounts        → SCR-22 Locked Account Management

  /admin (RoleProtectedRoute: Super Admin)
    /dashboard              → SCR-21 Super Admin Dashboard
```

---

### 10.6 Shared / Global UI Components

| Component | Description |
|-----------|-------------|
| `AppNavbar` | Top navigation bar: logo, user avatar, role badge, notification bell with unread count, logout |
| `AppSidebar` | Role-filtered left sidebar navigation (links visible based on current user role; team links hidden for managers with no subordinates) |
| `StatusBadge` | Colour-coded status pill (Draft / Pending / Approved / Rejected / Cancelled / Revoked) |
| `LeaveBalanceCard` | Leave type card showing used/total with progress bar |
| `ConfirmDialog` | Generic confirmation modal (cancel, revoke, deactivate, unlock actions) |
| `RejectionReasonDialog` | Modal with mandatory reason textarea (reject and revoke actions) |
| `FileUpload` | File picker with type/size validation (PDF/JPG/PNG, 5MB) |
| `DateRangePicker` | MUI date range picker with public holiday highlights; weekends/holidays disabled for start & end dates |
| `EntityDropdown` | Reusable dropdown for entity links (Department, Reporting Manager, Leave Type) per EMP-02 |
| `TimeRangePicker` | Start/End time picker with worked-hours computation (Comp-Off Request) |
| `NotificationBell` | Navbar bell icon with real-time unread count via polling |
| `LoadingSpinner` | Overlay spinner for async operations |
| `EmptyState` | Illustrated empty state for lists with no results |
| `Breadcrumb` | Page breadcrumb derived from React Router |

---

### 10.7 State Management (Redux Toolkit Slices)

| Store Slice | Managed State |
|-------------|---------------|
| `auth` | current user, role, JWT token expiry, SSO status |
| `leaveBalances` | all leave type balances for current user (cached, invalidated on changes) |
| `leaveRequests` | own leave request list + selected request detail |
| `compOffRequests` | own comp-off requests + pending comp-off approvals (Manager/HR) |
| `approvals` | pending approvals list for manager/HR (with pagination) |
| `notifications` | notification list, unread count |
| `holidays` | holiday list for current year |
| `employees` | employee list (HR/Admin; own team for Managers, paginated) |
| `departments` | department list (readable by all roles, writable by HR/Admin only, paginated) |
| `reports` | report filters and generated report data |
| `lockedAccounts` | locked account list (HR/Admin only) |

---

### 10.8 Frontend Non-Functional Requirements

| ID | Requirement |
|----|-------------|
| FE-NFR-01 | React application shall be served as a static SPA via Nginx |
| FE-NFR-02 | All routes except `/auth/*` shall be protected by `ProtectedRoute`; role-restricted routes by `RoleProtectedRoute` |
| FE-NFR-03 | JWT access token shall be stored in memory only (not localStorage); refresh token via HttpOnly cookie |
| FE-NFR-04 | Axios interceptor shall attach Bearer token to every API request and handle 401 by triggering silent refresh |
| FE-NFR-05 | Application shall display role-appropriate navigation — menu items not accessible to the user's role shall not be rendered |
| FE-NFR-06 | All forms shall use React Hook Form with client-side validation and clear error messages |
| FE-NFR-07 | Application shall handle API error responses gracefully: 400 (validation), 403 (permission), 404 (not found), 500 (server error) with user-friendly messages |
| FE-NFR-08 | Leave balance data shall be refreshed from the store after any leave apply / cancel / revoke / comp-off approval action |
| FE-NFR-09 | Notification unread count shall be polled every 60 seconds |
| FE-NFR-10 | Minimum supported browsers: Chrome 120+, Edge 120+, Firefox 121+ (desktop only for Phase 1) |

---

## 11. Out of Scope (Phase 1)

- Mobile application
- Payroll integration and salary deduction
- Time tracking and attendance management
- Leave encashment processing
- Multi-language / internationalization
- Multi-currency
- Outlook calendar integration (Google Calendar only in Phase 1)
- PDF report export (CSV only)
- S3 file storage (local filesystem in Phase 1)
- Multi-timezone support (IST only in Phase 1)
- Past data migration from existing systems
- Carry-forward of leave balances
- Negative-balance leave (requests are blocked when balance is insufficient)
- Leave application for HR Admin / Super Admin roles
- Configurable comp-off expiry (fixed at 30 days)
- Leave accrual schedules (monthly/quarterly) — annual lump-sum credit only
- Optional / regional holidays
- Bulk approve/reject
- Approval delegation and auto-escalation to next level (reminder emails only)
- Admin UI for system configuration (Azure AD role mapping etc. via application configuration)

---

## 12. Assumptions

| # | Assumption |
|---|-----------|
| A1 | Single organization deployment — no multi-tenancy |
| A2 | Single timezone (IST) for Phase 1 |
| A3 | Leave year is Jan 1 to Dec 31, not configurable |
| A4 | Maximum 500 concurrent users |
| A5 | Attachment storage on local filesystem is sufficient for Phase 1 |
| A6 | Azure AD is the only SSO provider needed |
| A7 | English only — no localization |
| A8 | All employees are salaried (no hourly/contract worker leave rules) |
| A9 | Weekend is fixed as Saturday and Sunday for all employees |
| A10 | Single holiday list applies to the whole organization (no regions) |

---

## 13. Constraints

| # | Constraint |
|---|-----------|
| C1 | Must use ASP.NET Core (.NET 8) + PostgreSQL (existing team expertise) |
| C2 | Must integrate with Azure AD (corporate standard) |
| C3 | Audit trail retention minimum 3 years (regulatory) |
| C4 | No employee data may be permanently deleted without anonymization (GDPR) |
