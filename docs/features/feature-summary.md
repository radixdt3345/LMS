# Feature Summary — Leave Management System (LMS)

**Derived from:** `docs/prd.md` (84 FRs, 65 ACs, 15 USs)
**Date:** July 2026

---

## Feature Index

| ID | Name | Wave | Priority | Depends On | HITL Flag |
|----|------|------|----------|------------|-----------|
| F-01 | User Authentication | 1 | MUST | NONE | NO |
| F-02 | Account Lockout Management | 1 | MUST | F-01 | NO |
| F-03 | Initial Data Seeding | 1 | MUST | NONE | NO |
| F-04 | Department Management | 1 | MUST | F-03 | NO |
| F-05 | Employee Management | 1 | MUST | F-01, F-03, F-04 | YES |
| F-06 | Leave Types and Policies | 1 | MUST | F-03 | NO |
| F-07 | Leave Balance Management | 1 | MUST | F-05, F-06 | NO |
| F-08 | Public Holiday Calendar | 1 | MUST | F-01, F-05 | NO |
| F-09 | Leave Request Workflow | 2 | MUST | F-01, F-05, F-06, F-07, F-08 | YES |
| F-10 | Comp-Off Request Workflow | 2 | MUST | F-05, F-07, F-08 | NO |
| F-11 | Approval Engine | 2 | MUST | F-09, F-05 | YES |
| F-12 | Notifications | 2 | MUST | F-09, F-10, F-11 | YES |
| F-13 | Dashboards and Reporting | 3 | MUST | F-07, F-09, F-10, F-11, F-12 | NO |
| F-14 | Audit Trail | 2 | MUST | F-01 | NO |

---

## Execution Waves

### Wave 1 — Foundation
Features that must exist before any user-facing functionality can be built or tested.

- **F-01 User Authentication** — JWT issuance, SSO, local login, refresh/logout. All other features require authenticated users.
- **F-02 Account Lockout Management** — Lock/unlock flow; security baseline.
- **F-03 Initial Data Seeding** — Default Super Admin, HR Admin, department, and 5 leave types.
- **F-04 Department Management** — CRUD for departments; FK target for employees.
- **F-05 Employee Management** — Employee CRUD, role auto-derivation, reporting structure; FK target for leave requests and balances.
- **F-06 Leave Types and Policies** — Configurable leave types; drives balance allocation and L2 routing.
- **F-07 Leave Balance Management** — Per-employee balances, proration, Hangfire jobs for expiry/lapse/new-year credit.
- **F-08 Public Holiday Calendar** — Holiday list used by sandwich rule, date validation, comp-off eligibility, and calendar UI.

Build order within Wave 1: F-03 → F-04 → F-05 → F-06 → F-07 → F-08 (F-01 and F-02 are parallel prerequisites).

### Wave 2 — Core
User-facing workflow features that depend on the Wave 1 foundation being fully operational.

- **F-09 Leave Request Workflow** — Application, draft, sandwich rule, all validation, cancel, revoke.
- **F-10 Comp-Off Request Workflow** — Comp-off submit, approve, credit lifecycle.
- **F-11 Approval Engine** — L1/L2 routing, no-manager handling, escalation Hangfire job.
- **F-12 Notifications** — SendGrid email, in-app notification center, Google Calendar sync.
- **F-14 Audit Trail** — Append-only log; cross-cutting; built alongside F-09 as AuditService is called by all services.

Build order within Wave 2: F-09 + F-14 (parallel) → F-10 → F-11 → F-12.

### Wave 3 — Enhancement
Read-heavy, data-aggregation features that require Wave 2 to have populated data.

- **F-13 Dashboards and Reporting** — Employee/Manager/HR Admin/Super Admin dashboards, charts, CSV export.

---

## FR Coverage

| FR-ID | Feature | Domain |
|-------|---------|--------|
| FR-1 | F-01 | Auth |
| FR-2 | F-01 | Auth |
| FR-3 | F-01 | Auth |
| FR-4 | F-01 | Auth |
| FR-5 | F-01 | Auth |
| FR-6 | F-01 | Auth |
| FR-7 | F-02 | Auth |
| FR-8 | F-02 | Auth |
| FR-9 | F-01 | Auth |
| FR-10 | F-01 | Auth |
| FR-11 | F-01 | Auth |
| FR-12 | F-05 | People |
| FR-13 | F-05 | People |
| FR-14 | F-05 | People |
| FR-15 | F-05 | People |
| FR-16 | F-05 | People |
| FR-17 | F-05 | People |
| FR-18 | F-05 | People |
| FR-19 | F-05 | People |
| FR-20 | F-05 | People |
| FR-21 | F-04 | People |
| FR-22 | F-04 | People |
| FR-23 | F-04 | People |
| FR-24 | F-04 | People |
| FR-25 | F-04 | People |
| FR-26 | F-04 | People |
| FR-27 | F-06 | LeaveCore |
| FR-28 | F-03, F-06 | LeaveCore |
| FR-29 | F-06 | LeaveCore |
| FR-30 | F-06 | LeaveCore |
| FR-31 | F-07 | LeaveCore |
| FR-32 | F-07 | LeaveCore |
| FR-33 | F-07 | LeaveCore |
| FR-34 | F-07 | CompOff |
| FR-35 | F-07 | CompOff |
| FR-36 | F-07 | CompOff |
| FR-37 | F-07 | LeaveCore |
| FR-38 | F-07 | LeaveCore |
| FR-39 | F-09 | LeaveCore |
| FR-40 | F-09 | LeaveCore |
| FR-41 | F-09 | LeaveCore |
| FR-42 | F-09 | LeaveCore |
| FR-43 | F-09 | LeaveCore |
| FR-44 | F-09 | LeaveCore |
| FR-45 | F-09 | LeaveCore |
| FR-46 | F-09 | LeaveCore |
| FR-47 | F-09 | LeaveCore |
| FR-48 | F-09, F-11 | LeaveCore |
| FR-49 | F-09 | LeaveCore |
| FR-50 | F-09, F-12 | Notifications |
| FR-51 | F-10 | CompOff |
| FR-52 | F-10 | CompOff |
| FR-53 | F-10 | CompOff |
| FR-54 | F-10 | CompOff |
| FR-55 | F-10 | CompOff |
| FR-56 | F-10 | CompOff |
| FR-57 | F-10, F-14 | CompOff |
| FR-58 | F-11 | LeaveCore |
| FR-59 | F-11 | LeaveCore |
| FR-60 | F-11 | LeaveCore |
| FR-61 | F-11, F-12 | Notifications |
| FR-62 | F-08 | Scheduling |
| FR-63 | F-08 | Scheduling |
| FR-64 | F-08 | Scheduling |
| FR-65 | F-08, F-09 | Scheduling |
| FR-66 | F-12 | Notifications |
| FR-67 | F-12 | Notifications |
| FR-68 | F-12 | Notifications |
| FR-69 | F-12 | Notifications |
| FR-70 | F-12 | Notifications |
| FR-71 | F-12 | Notifications |
| FR-72 | F-13 | Reporting |
| FR-73 | F-13 | Reporting |
| FR-74 | F-13 | Reporting |
| FR-75 | F-13 | Reporting |
| FR-76 | F-13 | Reporting |
| FR-77 | F-14 | Reporting |
| FR-78 | F-14 | Reporting |
| FR-79 | F-14 | Reporting |
| FR-80 | F-14 | Reporting |
| FR-81 | F-14 | Reporting |
| FR-82 | F-03 | Auth |
| FR-83 | F-03 | Auth |
| FR-84 | F-03 | Auth |

**Total FR Coverage: 84 / 84 (100%)**

---

## AC Coverage

| AC-ID | Feature.Story | AC-ID | Feature.Story |
|-------|--------------|-------|--------------|
| AC-1 | F-01.US-01.1 | AC-34 | F-09.US-09.1 |
| AC-2 | F-01.US-01.1 | AC-35 | F-09.US-09.1 |
| AC-3 | F-01.US-01.2 | AC-36 | F-09.US-09.2 |
| AC-4 | F-01.US-01.2 | AC-37 | F-09.US-09.3 |
| AC-5 | F-01.US-01.2 | AC-38 | F-09.US-09.3 |
| AC-6 | F-01.US-01.2 | AC-39 | F-09.US-09.1 + F-11.US-11.2 |
| AC-7 | F-02.US-02.1 | AC-40 | F-10.US-10.1 |
| AC-8 | F-02.US-02.1 | AC-41 | F-10.US-10.1 |
| AC-9 | F-02.US-02.2 | AC-42 | F-10.US-10.1 |
| AC-10 | F-01.US-01.1 | AC-43 | F-10.US-10.1 |
| AC-11 | F-05.US-05.1 | AC-44 | F-11.US-11.1 |
| AC-12 | F-05.US-05.1 | AC-45 | F-11.US-11.1 |
| AC-13 | F-05.US-05.1 | AC-46 | F-11.US-11.2 |
| AC-14 | F-05.US-05.1 | AC-47 | F-11.US-11.2 |
| AC-15 | F-05.US-05.1 | AC-48 | F-11.US-11.3 |
| AC-16 | F-05.US-05.1 | AC-49 | F-08.US-08.1 |
| AC-17 | F-05.US-05.2 | AC-50 | F-08.US-08.1 |
| AC-18 | F-04.US-04.1 | AC-51 | F-08.US-08.1 + F-09.US-09.1 |
| AC-19 | F-04.US-04.1 | AC-52 | F-12.US-12.1 |
| AC-20 | F-06.US-06.1 | AC-53 | F-12.US-12.1 |
| AC-21 | F-07.US-07.1 | AC-54 | F-12.US-12.2 |
| AC-22 | F-07.US-07.2 | AC-55 | F-12.US-12.3 |
| AC-23 | F-07.US-07.2 | AC-56 | F-12.US-12.3 |
| AC-24 | F-07.US-07.1 | AC-57 | F-07.US-07.1 + F-13.US-13.1 |
| AC-25 | F-07.US-07.3 | AC-58 | F-13.US-13.2 |
| AC-26 | F-07.US-07.3 | AC-59 | F-13.US-13.3 |
| AC-27 | F-09.US-09.1 | AC-60 | F-13.US-13.5 |
| AC-28 | F-09.US-09.1 | AC-61 | F-14.US-14.1 |
| AC-29 | F-09.US-09.1 | AC-62 | F-14.US-14.1 |
| AC-30 | F-09.US-09.1 | AC-63 | F-14.US-14.1 |
| AC-31 | F-09.US-09.1 | AC-64 | F-03.US-03.1 |
| AC-32 | F-09.US-09.1 | AC-65 | F-03.US-03.1 |
| AC-33 | F-09.US-09.1 | | |

**Total AC Coverage: 65 / 65 (100%)**

---

## HITL-Flagged Features Summary

| Feature | Flag Reason |
|---------|------------|
| **F-05** Employee Management | Role auto-derivation idempotency: confirm that promoting a user who is already a Manager is a no-op (no error, no duplicate events). |
| **F-09** Leave Request Workflow | Sandwich rule applies to a single request's date range only. Non-working days bridging two separate requests are never counted. Confirm this interpretation. |
| **F-11** Approval Engine | ✅ RESOLVED: No-manager rule always wins. HR Admin as L1 skips L2 unconditionally, including retroactive requests. |
| **F-12** Notifications | ✅ RESOLVED: Google Calendar uses company-wide service account, not per-user OAuth2. Single credential, shared company calendar. |
