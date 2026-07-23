# Discovery — Leave Management System (LMS)

**Version**: 1.3 (derived from approved requirements document)
**Date**: July 2026
**Status**: Confirmed

---

## Round 1 — Problem & Vision

### Problem Statement
Manual leave tracking via spreadsheets, emails, and verbal approvals creates bottlenecks, lack of visibility, inconsistent policy enforcement, and non-auditable records. HR spends significant time chasing approvals and reconciling data.

### Vision
An enterprise web application that automates the full leave lifecycle — from employee application through multi-level approval, balance tracking, and compliance reporting — for a single-organization deployment.

### Success Criteria
- Zero manual leave tracking processes
- All policy rules enforced automatically (sandwich rule, balance limits, overlap limits)
- Real-time team availability visible to managers
- Full audit trail retained for 3+ years
- 99.9% uptime, p99 API response < 500ms

---

## Round 2 — Users & Roles

### Role Registry

| Role | Description | Source |
|------|-------------|--------|
| Employee | Any staff member — applies for leave, views own balance/history | Auto-assigned on account creation |
| Manager | Team lead — approves/rejects direct reports' leave and comp-off, views team calendar | Auto-derived from reporting structure |
| HR Admin | HR team — manages leave types, holidays, employees, reporting; handles L2 approvals, revocations | Assigned by Super Admin |
| Super Admin | Full system access — all data, audit logs, locked account management | Assigned during setup |

### Role Hierarchy
```
Super Admin → HR Admin → Manager → Employee
```

Higher roles inherit lower-role permissions **except** personal leave screens (Apply for Leave, Employee Dashboard, My Leave Balances, My Leave History, My Profile, Cancel Leave Request, Comp-Off Request) — these are Employee and Manager only.

HR Admin and Super Admin **cannot apply for leave** in the system.

### Reporting Structure
- Employee optionally has one reporting Manager
- If no reporting manager: all leave/comp-off notifications and L1 approvals route to HR Admin
- Manager is also an Employee (can apply for leave; approved by their own manager or HR Admin)
- Manager/Employee sees subordinate views only when at least one employee reports to them

---

## Round 3 — Features & Per-Feature RBAC

### F1 — Authentication & SSO
- Azure AD SSO via OAuth2 authorization code flow
- Local email+password fallback
- JWT (24h) + refresh token (7d, stored in DB)
- Account locks after 3 failed local attempts
- **RBAC**: All roles; locked account unlock by HR Admin / Super Admin only

### F2 — Employee Management
- CRUD on employee profiles (name, email, department, designation, DOJ, reporting manager, status)
- Soft delete; role auto-derived from reporting structure
- **RBAC**: HR Admin / Super Admin — full CRUD; Manager — read own team; Employee — read/edit own profile (name, phone only)

### F3 — Department Management
- Flat department list with name, code, team overlap limit, status
- Soft delete; unique name+code
- **RBAC**: HR Admin / Super Admin — CRUD; Manager / Employee — read-only

### F4 — Leave Types & Policies
- Configurable leave types: Casual (12d), Sick (6d+attachment+HR flag), Earned (1d), Comp-off (0, credit only), Unpaid (0, no balance)
- Annual lump-sum credit Jan 1; prorated for mid-year joiners
- No carry-forward
- **RBAC**: HR Admin / Super Admin — CRUD; all roles — read

### F5 — Leave Balance Management
- Per-employee, per-leave-type balance; real-time update on approval/cancel/revoke
- Half-day = 0.5 days deducted
- Comp-off credits via approved comp-off requests; expire 30 days from earn date
- Daily Hangfire job for comp-off expiry; yearly job for Dec 31 year-end lapse
- **RBAC**: Employee/Manager — own balances; Manager — own team balances; HR Admin/Super Admin — department/all

### F6 — Leave Request & Workflow
- Apply: leave type, date range, half-day flag, reason, attachment (when required)
- Save as draft; validate on submit (balance, overlap, team limit, weekends/holidays, sandwich rule)
- Status lifecycle: Draft → Submitted → Pending L1 → L1 Approved → [Pending L2] → Approved → Active → Completed
- Cancel (employee, before start date); Revoke (HR Admin, before start date)
- Retroactive requests → mandatory L2 approval
- **RBAC**: Employee/Manager — apply; Manager — L1 approve own team; HR Admin — L2 approve, revoke; Super Admin — view all

### F7 — Comp-Off Requests
- Submit: date (must be weekend/holiday), description, IsHalfDay, start/end time
- Validation: >4h → half-day eligible; ≥8h → full-day eligible; <4h → blocked
- Cannot be cancelled by employee once submitted
- Approved by reporting manager or HR Admin
- **RBAC**: Employee/Manager — submit; Manager/HR Admin — approve/reject

### F8 — Approval Engine
- L1: reporting manager (or HR Admin if none)
- L2: required when duration > 3 days OR RequiresHRFlag=Yes OR retroactive; only after L1 complete
- When HR Admin acts as L1 (no manager), L2 is skipped entirely
- Escalation: reminder email every 2 days via daily Hangfire job
- **RBAC**: Manager — L1; HR Admin — L2

### F9 — Public Holiday Calendar
- HR Admin manages holidays (date, name); bulk CSV import
- Used in: sandwich rule, working-day counting, team overlap, comp-off eligibility
- **RBAC**: All — read; HR Admin / Super Admin — CRUD

### F10 — Notifications
- Email (SendGrid v3): leave applied, approved, rejected, cancelled, revoked, escalation reminders, comp-off applied/approved/rejected
- In-app notification center: read/unread, click to navigate
- Google Calendar sync: create all-day event on approval, delete on cancel/revoke (Hangfire, 3 retries)
- SendGrid failure: Hangfire retry 5x over 24h
- **RBAC**: All — own notifications

### F11 — Reporting & Dashboards
- Employee Dashboard: balance cards, recent history, upcoming holidays
- Manager Dashboard: team calendar, pending approvals count, team balance summary
- HR Dashboard: dept utilization chart, monthly trend chart, compliance report, CSV export
- Super Admin Dashboard: system-wide metrics, full audit log viewer
- **RBAC**: Per role as above

### F12 — Audit Trail
- Append-only log: who, what (old→new), when, IP
- All state-changing actions logged
- 3-year retention
- **RBAC**: HR Admin / Super Admin — full search and view

### F13 — Initial Data Seeding
- Two default users: Super Admin + HR Admin (password: Admin@123, local login)
- Default department: HR
- Default leave types: Casual, Sick, Earned, Comp-off, Unpaid
- Idempotent seed script

---

## Round 4 — Technical Decisions

### Stack
| Layer | Technology |
|-------|-----------|
| Backend | C# 12, .NET 8, ASP.NET Core Web API |
| Database | PostgreSQL 15+ |
| Job Queue | Hangfire (PostgreSQL storage) |
| Auth | JWT + Azure AD OAuth2 (MSAL) |
| Email | SendGrid API v3 |
| Calendar | Google Calendar API v3 |
| ORM/Migrations | EF Core + EF Core Migrations |
| Testing | xUnit |
| Frontend | React 17 (Functional Components + Hooks) |
| UI Library | MUI (Material-UI) v5 |
| State Management | Redux Toolkit + Redux-Saga |
| HTTP Client | Axios + Interceptors |
| Routing | React Router with Protected Routes |
| Charts | Chart.js via react-chartjs-2 |
| Calendar UI | FullCalendar (React wrapper) |
| Frontend Auth | MSAL React (Azure AD SSO) |
| Build/Deploy | Vite + Nginx (frontend); Docker (backend) |

### Architecture
- Single-org deployment (no multi-tenancy)
- IST timezone only (Phase 1)
- REST API (versioned: /api/v1/)
- JWT in memory (frontend) + HttpOnly cookie (refresh token)
- Local filesystem attachment storage (Phase 1; S3 in Phase 2)
- API rate limiting: 100 req/min per user
- p50 < 200ms, p99 < 500ms (CRUD); p99 < 2s (reports)

---

## Round 5 — Constraints & Risks

### Constraints
- Must use ASP.NET Core (.NET 8) + PostgreSQL (team expertise)
- Must integrate with Azure AD (corporate standard)
- Audit trail retention: minimum 3 years (regulatory)
- No permanent employee data deletion without anonymization (GDPR)

### Key Risks
- Sandwich rule complexity: algorithm must handle edge cases precisely (see REQ-04)
- Comp-off expiry: daily Hangfire job must be reliable
- Google Calendar OAuth per-user: requires user-consent flow on first use
- Azure AD role mapping: maintained in app config, no admin UI in Phase 1

### Out of Scope (Phase 1)
- Mobile app, payroll integration, time tracking, leave encashment
- Multi-language, multi-currency, multi-timezone
- Outlook calendar, PDF export, S3 storage
- Carry-forward, negative balances, leave for HR Admin/Super Admin
- Bulk approve/reject, approval delegation

---

## Final Confirmation

### Role Registry (confirmed)
Employee, Manager (auto-derived), HR Admin, Super Admin

### Permission Matrix (summary)

| Resource | Employee | Manager | HR Admin | Super Admin |
|----------|----------|---------|----------|-------------|
| Apply Leave | ✅ | ✅ | ❌ | ❌ |
| L1 Approve | ❌ | ✅ (own team) | ✅ (if no manager) | ❌ |
| L2 Approve | ❌ | ❌ | ✅ | ❌ |
| Revoke Leave | ❌ | ❌ | ✅ | ✅ |
| Employee CRUD | ❌ | 👁 own team | ✅ | ✅ |
| Department CRUD | 👁 | 👁 | ✅ | ✅ |
| Leave Type CRUD | 👁 | 👁 | ✅ | ✅ |
| Holiday CRUD | 👁 | 👁 | ✅ | ✅ |
| Audit Log | ❌ | ❌ | ✅ | ✅ |
| Locked Accounts | ❌ | ❌ | ✅ | ✅ |
| Reports | Own | Own team | Dept/All | All |

### 22 Screens confirmed
SCR-01 Login, SCR-02 Employee Dashboard, SCR-03 My Profile, SCR-04 Apply for Leave, SCR-05 My Leave History, SCR-06 Leave Request Detail, SCR-07 My Leave Balances, SCR-08 Holiday Calendar, SCR-09 Comp-Off Request, SCR-10 Notification Center, SCR-11 Manager Dashboard, SCR-12 Pending Approvals List, SCR-13 Team Calendar, SCR-14 HR Dashboard, SCR-15 Employee List & Management, SCR-16 Create/Edit Employee, SCR-16a Department List, SCR-16b Create/Edit Department, SCR-17 Leave Type Management, SCR-18 Holiday Management, SCR-19 Department Reports, SCR-20 Audit Log Viewer, SCR-21 Super Admin Dashboard, SCR-22 Locked Account Management

### Domains (bounded contexts)
1. **Auth** — authentication, JWT, SSO, account locking
2. **People** — employees, departments, roles, reporting structure
3. **LeaveCore** — leave types, balances, leave requests, workflow, approval engine
4. **CompOff** — comp-off requests, credits, expiry
5. **Scheduling** — Hangfire jobs (expiry, escalation, calendar sync, year-end lapse)
6. **Notifications** — in-app + email (SendGrid) + calendar (Google Calendar)
7. **Reporting** — dashboards, charts, reports, audit trail
