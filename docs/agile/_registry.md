# Agile Structure Registry — LMS V5
Generated: 2026-07-23 | Status: Awaiting HIL 6 confirmation

---

## Summary
| Level | Count |
|-------|-------|
| Epics | 14 |
| User Stories | 37 |
| Tasks | 77 |
| Subtasks | 0 (reserved) |

---

## Epic → Story → Task Map

### E-01 — User Authentication (F-01, Wave 1, AUTH)
| Story | Title |
|-------|-------|
| US-01.1 | As an employee, I want to sign in with my Microsoft account (Azure AD SSO) so that I don't need a separate password |
| US-01.2 | As an employee, I want to sign in with email and password so that I can access the system without SSO |
| US-01.3 | As an authenticated user, I want my session to refresh silently so that I'm not logged out mid-session |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-001 | AUTH-DB-001 | DB | Create users and refresh_tokens tables |
| T-002 | AUTH-API-001 | API | Local login endpoint (POST /api/v1/auth/login) |
| T-003 | AUTH-API-002 | API | Azure AD SSO callback |
| T-004 | AUTH-API-003 | API | Token refresh and logout endpoints |
| T-005 | AUTH-UI-001 | UI | Login page and auth flow (React + MSAL) |
| T-006 | AUTH-INT-001 | INT | Wire JWT middleware, CORS, rate-limiting |
| T-007 | AUTH-TEST-001 | TEST | Integration tests IT-1 to IT-6 |
| T-008 | AUTH-E2E-001 | E2E | E2E: Login flows (E2E-1, E2E-2) |

---

### E-02 — Account Lockout Management (F-02, Wave 1, AUTH)
| Story | Title |
|-------|-------|
| US-02.1 | As the system, I want to lock accounts after 5 failed login attempts so that brute-force attacks are prevented |
| US-02.2 | As an HR Admin, I want to unlock locked accounts so that employees can regain access |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-009 | AUTH-API-004 | API | Account lockout and unlock endpoints |
| T-010 | AUTH-UI-002 | UI | Locked Accounts management page |
| T-011 | AUTH-INT-002 | INT | Integration tests IT-7, IT-8 |
| T-012 | AUTH-TEST-002 | TEST | E2E: Locked account UI flow (E2E-3) |

---

### E-03 — Initial Data Seeding (F-03, Wave 1, INFRA)
| Story | Title |
|-------|-------|
| US-03.1 | As a system operator, I want the system to bootstrap with a SuperAdmin, HR Admin, default department, and 5 leave types on first startup so that the system is immediately usable |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-013 | INFRA-DB-001 | DB | Idempotent data seeder (SeedService) |
| T-014 | INFRA-TEST-001 | TEST | Integration tests IT-45, IT-46 |
| T-015 | INFRA-E2E-001 | E2E | E2E: Seeded SuperAdmin can login (E2E-14) |

---

### E-04 — Department Management (F-04, Wave 1, PEOPLE)
| Story | Title |
|-------|-------|
| US-04.1 | As an HR Admin, I want to create, edit, and soft-delete departments so that the org structure is maintained |
| US-04.2 | As any authenticated user, I want to view the department list so that I can select departments in forms |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-016 | PEOPLE-DB-001 | DB | Create departments table |
| T-017 | PEOPLE-API-001 | API | Department CRUD endpoints |
| T-018 | PEOPLE-UI-001 | UI | Department management page |
| T-019 | PEOPLE-INT-001 | INT | Wire DepartmentService + IMemoryCache |
| T-020 | PEOPLE-TEST-001 | TEST | Integration tests IT-9, IT-10 |

---

### E-05 — Employee Management (F-05, Wave 1, PEOPLE)
| Story | Title |
|-------|-------|
| US-05.1 | As an HR Admin, I want to create, edit, and deactivate employees so that the workforce is accurately represented |
| US-05.2 | As a Manager, I want to view my direct team so that I can manage their leave requests |
| US-05.3 | As an Employee, I want to view and edit my own profile so that my information is current |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-021 | PEOPLE-DB-002 | DB | Add profile columns + manager self-ref FK to users |
| T-022 | PEOPLE-API-002 | API | Employee CRUD and role-derivation endpoints |
| T-023 | PEOPLE-UI-002 | UI | Employee management and profile pages |
| T-024 | PEOPLE-INT-002 | INT | Wire EmployeeService + role auto-derivation |
| T-025 | PEOPLE-TEST-002 | TEST | Integration tests IT-11 to IT-15 |

---

### E-06 — Leave Types and Policies (F-06, Wave 1, LEAVECORE)
| Story | Title |
|-------|-------|
| US-06.1 | As an HR Admin, I want to create and configure leave types with accrual rules so that the leave policy is enforced automatically |
| US-06.2 | As an Employee, I want to view available leave types so that I know which leaves I can apply for |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-026 | LEAVECORE-DB-001 | DB | Create leave_types table |
| T-027 | LEAVECORE-API-001 | API | Leave Type CRUD endpoints |
| T-028 | LEAVECORE-UI-001 | UI | Leave Types management page |
| T-029 | LEAVECORE-INT-001 | INT | Integration test IT-16 + cache wiring |
| T-030 | LEAVECORE-TEST-001 | TEST | Unit test UT-21 |

---

### E-07 — Leave Balance Management (F-07, Wave 1, LEAVECORE)
| Story | Title |
|-------|-------|
| US-07.1 | As an Employee, I want to view my current leave balances so that I know how many days I have remaining |
| US-07.2 | As the system, I want to credit annual leave balances on January 1st and prorate for new joiners so that balances are always accurate |
| US-07.3 | As the system, I want to lapse non-carry-forward balances at year-end and expire comp-off credits after 180 days via scheduled jobs |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-031 | LEAVECORE-DB-002 | DB | Create leave_balances table + Hangfire schema |
| T-032 | LEAVECORE-API-002 | API | LeaveBalanceService + Hangfire jobs |
| T-033 | LEAVECORE-UI-002 | UI | Leave Balance display widget |
| T-034 | LEAVECORE-INT-002 | INT | Integration tests IT-17, IT-18 |
| T-035 | LEAVECORE-TEST-002 | TEST | Unit tests UT-22 to UT-31 |

---

### E-08 — Public Holiday Calendar (F-08, Wave 1, LEAVECORE)
| Story | Title |
|-------|-------|
| US-08.1 | As an Employee, I want to view the company holiday calendar so that I can plan my leaves |
| US-08.2 | As an HR Admin, I want to add, delete, and bulk-import holidays via CSV so that the calendar stays up to date |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-036 | LEAVECORE-DB-003 | DB | Create holidays table |
| T-037 | LEAVECORE-API-003 | API | Holiday CRUD + bulk CSV import |
| T-038 | LEAVECORE-UI-003 | UI | Holiday Calendar page (FullCalendar) |
| T-039 | LEAVECORE-INT-003 | INT | Integration tests IT-19, IT-20 |
| T-040 | LEAVECORE-TEST-003 | TEST | Unit tests UT-32, UT-33 + E2E-10 |

---

### E-09 — Leave Request Workflow (F-09, Wave 2, LEAVECORE)
| Story | Title |
|-------|-------|
| US-09.1 | As an Employee, I want to submit a leave request with dates and reason, with sandwich rule applied, so that my balance is correctly computed |
| US-09.2 | As an Employee, I want to cancel my pending leave request so that my balance is restored |
| US-09.3 | As an Employee, I want to view my complete leave history so that I can track my usage |
| US-09.4 | As an HR Admin, I want to revoke an approved leave request so that errors can be corrected |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-041 | LEAVECORE-DB-004 | DB | Create leave_requests + approval_steps tables |
| T-042 | LEAVECORE-API-004 | API | Leave Request CRUD + SandwichRuleEngine |
| T-043 | LEAVECORE-UI-004 | UI | Leave Request form and history page |
| T-044 | LEAVECORE-INT-004 | INT | Integration tests IT-21 to IT-32 |
| T-045 | LEAVECORE-TEST-004 | TEST | Unit tests UT-34 to UT-42 (sandwich rule) |
| T-046 | LEAVECORE-E2E-001 | E2E | E2E: Leave application (E2E-4, E2E-9) |

---

### E-10 — Comp-Off Request Workflow (F-10, Wave 2, COMPOFF)
| Story | Title |
|-------|-------|
| US-10.1 | As an Employee, I want to submit a comp-off request for a worked holiday so that I earn additional leave credit |
| US-10.2 | As a Manager, I want to approve or reject comp-off requests from my team so that credits are awarded accurately |
| US-10.3 | As an Employee, I want to view my comp-off credits and their expiry dates so that I can use them before they lapse |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-047 | COMPOFF-DB-001 | DB | Create comp_off_requests + comp_off_credits tables |
| T-048 | COMPOFF-API-001 | API | Comp-Off request and credit endpoints |
| T-049 | COMPOFF-UI-001 | UI | Comp-Off request and credits UI |
| T-050 | COMPOFF-INT-001 | INT | Integration tests IT-33 to IT-36 |
| T-051 | COMPOFF-TEST-001 | TEST | Unit tests UT-43 to UT-47 + E2E-8 |
| T-052 | COMPOFF-E2E-001 | E2E | E2E runner config for E2E-8 |

---

### E-11 — Approval Engine (F-11, Wave 2, LEAVECORE)
| Story | Title |
|-------|-------|
| US-11.1 | As a Manager, I want to approve or reject leave requests from my direct reports (L1) so that the approval chain moves forward |
| US-11.2 | As an HR Admin, I want to provide L2 approval for cases requiring it (retroactive, policy-flagged) so that final approval is complete |
| US-11.3 | As an HR Admin, I want to be the sole approver for employees with no manager so that no request is stuck without a decision |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-053 | LEAVECORE-API-005 | API | Approval Engine (L1/L2 routing, no-manager rule) |
| T-054 | LEAVECORE-UI-005 | UI | Approval inbox UI |
| T-055 | LEAVECORE-INT-005 | INT | Integration tests IT-37 to IT-41 |
| T-056 | LEAVECORE-TEST-005 | TEST | Unit tests UT-48 to UT-53 + E2E-6, E2E-7 |
| T-057 | LEAVECORE-E2E-002 | E2E | E2E runner config for E2E-6, E2E-7 |

---

### E-12 — Notifications (F-12, Wave 2, NOTIFICATIONS)
| Story | Title |
|-------|-------|
| US-12.1 | As an Employee, I want to receive in-app notifications when my leave status changes so that I'm always informed |
| US-12.2 | As an Employee, I want to receive an email via SendGrid when my leave is approved or rejected so that I have a record outside the app |
| US-12.3 | As an Employee, I want approved leave events to appear on the company Google Calendar so that the team's availability is visible |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-058 | NOTIFICATIONS-DB-001 | DB | Create notifications table |
| T-059 | NOTIFICATIONS-API-001 | API | NotificationService + EmailService (SendGrid) + CalendarService |
| T-060 | NOTIFICATIONS-UI-001 | UI | Notification bell and polling |
| T-061 | NOTIFICATIONS-INT-001 | INT | Integration tests IT-42 to IT-44 |
| T-062 | NOTIFICATIONS-TEST-001 | TEST | Unit tests UT-54, UT-55 + E2E-11 |
| T-063 | NOTIFICATIONS-E2E-001 | E2E | E2E runner config for E2E-11 |

---

### E-13 — Dashboards and Reporting (F-13, Wave 3, REPORTING)
| Story | Title |
|-------|-------|
| US-13.1 | As an Employee, I want a dashboard showing my leave balances, upcoming leaves, and quick-apply access |
| US-13.2 | As a Manager, I want a dashboard showing my team's leave calendar, pending approvals, and headcount on leave today |
| US-13.3 | As an HR Admin, I want dashboards and reports showing org-wide utilization, trends, compliance, with CSV export |
| US-13.4 | As a Super Admin, I want a dashboard showing system health (Hangfire jobs), locked accounts, and recent audit events |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-070 | REPORTING-API-001 | API | ReportService + all dashboard data endpoints |
| T-071 | REPORTING-UI-001 | UI | Employee Dashboard |
| T-072 | REPORTING-UI-002 | UI | Manager Dashboard |
| T-073 | REPORTING-UI-003 | UI | HR Admin Dashboard and reports |
| T-074 | REPORTING-UI-004 | UI | Super Admin Dashboard |
| T-075 | REPORTING-INT-001 | INT | Integration tests IT-47, IT-48, IT-52, IT-53 |
| T-076 | REPORTING-TEST-001 | TEST | Unit tests UT-57 to UT-61 + E2E-12, E2E-5 |
| T-077 | REPORTING-E2E-001 | E2E | E2E runner config for E2E-12, E2E-5 |

---

### E-14 — Audit Trail (F-14, Wave 2, REPORTING)
| Story | Title |
|-------|-------|
| US-14.1 | As an HR Admin, I want to view an immutable audit log of all system actions so that changes are traceable |
| US-14.2 | As the system, I want every domain mutation to be logged to the audit trail so that a complete record exists |

Tasks:
| Task ID | Issue ID | Layer | Title |
|---------|----------|-------|-------|
| T-064 | REPORTING-DB-001 | DB | Create audit_logs table |
| T-065 | REPORTING-API-002 | API | AuditService + audit log query endpoint |
| T-066 | REPORTING-UI-005 | UI | Audit Trail page |
| T-067 | REPORTING-INT-002 | INT | Integration tests IT-49 to IT-51 |
| T-068 | REPORTING-TEST-002 | TEST | Unit test UT-56 + E2E-13 |
| T-069 | REPORTING-E2E-002 | E2E | E2E runner config for E2E-13 |

---

## Execution Order (Dependency-Based)

```
Wave 1 (parallel):
  E-01 → E-02 (AUTH-DB-001 blocks AUTH-API-004)
  E-03 (independent)
  E-04 → E-05 (PEOPLE-DB-001 blocks PEOPLE-DB-002)
  E-06 → E-07 → E-08 (leave_types blocks leave_balances)

Wave 2 (after all Wave 1 INT tasks COMPLETE):
  E-09, E-10, E-11 (LEAVECORE chain — E-09 → E-11)
  E-12 (after E-09, E-10, E-11)
  E-14 (after E-01, runs parallel to E-09 to E-12)

Wave 3 (after all Wave 2 INT tasks COMPLETE):
  E-13
```
