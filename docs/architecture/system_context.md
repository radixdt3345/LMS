# System Context — Leave Management System (LMS)

## System Boundary

```
╔══════════════════════════════════════════════════════════════════════════╗
║                    Leave Management System (LMS)                         ║
║                                                                          ║
║  ┌──────────────┐    ┌──────────────────┐    ┌─────────────────────┐   ║
║  │  React SPA   │◄──►│  ASP.NET Core    │◄──►│   PostgreSQL 15+    │   ║
║  │  (Nginx)     │    │  Web API         │    │   (data + Hangfire) │   ║
║  └──────────────┘    └──────────────────┘    └─────────────────────┘   ║
║                              │                                           ║
║                    ┌─────────┴─────────┐                                ║
║                    │  Hangfire Jobs    │                                  ║
║                    │  (background)     │                                  ║
║                    └───────────────────┘                                 ║
╚══════════════════════════════════════════════════════════════════════════╝
          │                    │                         │
          ▼                    ▼                         ▼
  ┌──────────────┐   ┌──────────────────┐    ┌─────────────────────┐
  │  Azure AD    │   │    SendGrid      │    │  Google Calendar    │
  │  (Entra ID)  │   │  (Email API v3)  │    │  API v3 (per user)  │
  └──────────────┘   └──────────────────┘    └─────────────────────┘
```

## External Actors

| Actor | Interaction with LMS | Direction |
|-------|---------------------|-----------|
| Employee | Login, apply leave, view balances, comp-off, notifications | Browser → SPA → API |
| Manager | L1 approvals, team calendar, comp-off approvals | Browser → SPA → API |
| HR Admin | L2 approvals, employee/dept/leave-type management, reporting, audit | Browser → SPA → API |
| Super Admin | System-wide metrics, audit, locked accounts | Browser → SPA → API |
| Azure AD | SSO identity provider — issues auth tokens | API → Azure AD (OAuth2 redirect); Azure AD → API callback |
| SendGrid | Email delivery | API (Hangfire) → SendGrid |
| Google Calendar | Per-user leave event sync | API (Hangfire) → Google Calendar (OAuth2 per user) |

## What the System Exposes

| Surface | Description |
|---------|-------------|
| `GET /api/v1/auth/sso/login` | Redirects to Azure AD |
| `GET /api/v1/auth/sso/callback` | Handles Azure AD callback, issues JWT |
| `POST /api/v1/auth/login` | Local email+password login |
| `POST /api/v1/auth/refresh` | Refresh JWT via HttpOnly cookie |
| `POST /api/v1/auth/logout` | Invalidate refresh token |
| `/api/v1/employees/**` | Employee CRUD and profile |
| `/api/v1/departments/**` | Department CRUD |
| `/api/v1/leave-types/**` | Leave type CRUD |
| `/api/v1/balances/**` | Leave balance reads |
| `/api/v1/leave-requests/**` | Leave application lifecycle |
| `/api/v1/approvals/**` | L1/L2 approval actions |
| `/api/v1/comp-off-requests/**` | Comp-off submission and approval |
| `/api/v1/holidays/**` | Holiday CRUD and CSV import |
| `/api/v1/notifications/**` | In-app notifications |
| `/api/v1/reports/**` | Dashboards and CSV export |
| `/api/v1/audit-log` | Audit trail search |
| `/api/v1/accounts/locked` | Locked account management |
| `GET /health` | Health check (unauthenticated) |
| `/hangfire` | Hangfire dashboard (Super Admin, configurable) |

## What the System Consumes

| External Service | Purpose | Protocol |
|-----------------|---------|---------|
| Azure AD (Entra ID) | SSO OAuth2 Authorization Code Flow; group membership for role mapping | HTTPS / OAuth2 |
| SendGrid API v3 | Transactional email notifications | HTTPS / REST + API Key |
| Google Calendar API v3 | Per-user leave event create/delete | HTTPS / REST + OAuth2 per user |
| PostgreSQL 15+ | All application data + Hangfire job persistence | TCP (TLS) |

## Key Data Flows

### Leave Application Flow
```
Employee (browser) → POST /api/v1/leave-requests → API validates (balance, overlap,
  sandwich rule, team limit, holiday/weekend) → DB: insert LeaveRequest
  → Hangfire: enqueue email notification → SendGrid: email to manager
  → Manager (browser) → POST /api/v1/approvals/{id}/approve → DB: update status
  → [if L2 needed] → HR Admin approves → DB: status = Approved
  → DB: deduct balance → Hangfire: enqueue CalendarSyncJob
  → Google Calendar API: create all-day event
```

### SSO Login Flow
```
Employee (browser) → GET /api/v1/auth/sso/login → 302 → Azure AD
  → Azure AD: user authenticates → callback to GET /api/v1/auth/sso/callback
  → API: exchange code for tokens → validate AD group → map to LMS role
  → DB: upsert User → issue JWT (24h) + refresh token (7d, DB-stored)
  → Set HttpOnly cookie (refresh token) → return access_token in body
  → Browser: store JWT in memory → subsequent requests: Authorization: Bearer {token}
```

### Background Job Flow
```
Hangfire scheduler (daily) → EscalationJob: find pending approvals > 2 days old
  → SendGrid: reminder email to pending approver
  → CompOffExpiryJob: expire comp-off credits past expiry_date → DB: update balance
Dec 31: YearEndLapseJob → DB: zero all balances → AuditLog: lapse entries
Jan 1: NewYearCreditJob → DB: create new LeaveBalance rows for new year
```
