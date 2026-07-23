# Architecture Assumptions — Leave Management System (LMS)

All assumptions below are UNVERIFIED pending HIL 2 confirmation.

---

ASSUMPTION-1:
Statement: A single PostgreSQL 15+ instance is sufficient for Phase 1 (500 concurrent users). No read replicas or connection pooler (e.g. PgBouncer) is required.
Confidence: HIGH
Risk if wrong: DB becomes the bottleneck under peak load; connection pool exhaustion
Verification: Load test with 500 concurrent simulated sessions before go-live
Status: CONFIRMED

---

ASSUMPTION-2:
Statement: No Docker is used in Phase 1. The ASP.NET Core API runs directly on the host (IIS or `dotnet run` / Windows Service), the React SPA is served by a standalone Nginx or IIS static site, and PostgreSQL runs as a native install on the server.
Confidence: HIGH
Risk if wrong: Environment parity between dev and production must be maintained manually; no container isolation
Verification: CONFIRMED by user
Status: CONFIRMED — No Docker

---

ASSUMPTION-3:
Statement: Azure AD app registration with OAuth2 Authorization Code Flow + scopes (openid, profile, email, User.Read, GroupMember.Read.All) can be created and the LMS redirect URI will be approved by the organization's Azure AD admin.
Confidence: HIGH
Risk if wrong: SSO cannot be enabled; local login only (fallback is available but SSO is a MUST requirement)
Verification: Confirm with IT Admin / Azure AD admin that app registration has been or can be created (DEFERRED to HIL 7)
Status: CONFIRMED

---

ASSUMPTION-4:
Statement: SendGrid account with v3 API access is available. Emails will use plain text / inline HTML bodies — NOT SendGrid dynamic templates. No template IDs required in configuration.
Confidence: HIGH
Risk if wrong: None — simpler implementation, no external template management
Verification: CONFIRMED by user — use plain text emails
Status: CONFIRMED — SendGrid available; plain text / inline HTML emails (no dynamic templates)

---

ASSUMPTION-5:
Statement: Google Calendar API v3 OAuth2 credentials (client_id + client_secret) can be created via Google Cloud Console. The consent screen will be approved for the organization's domain or will operate in test mode.
Confidence: MEDIUM
Risk if wrong: Google Calendar sync cannot be enabled (non-blocking — FR-70 specifies this must not block leave approval)
Verification: Confirm with Google Workspace admin / IT (DEFERRED to HIL 7)
Status: CONFIRMED

---

ASSUMPTION-6:
Statement: Local filesystem attachment storage (Docker volume mount `./uploads:/app/uploads`) is sufficient for Phase 1. The volume is on a disk with adequate space (at least 10GB for Phase 1).
Confidence: HIGH
Risk if wrong: Attachment uploads fail when disk is full; file loss if volume is not backed up
Verification: Confirm disk size and backup policy with DevOps for the target server
Status: CONFIRMED

---

ASSUMPTION-7:
Statement: IST (UTC+5:30) is the single timezone for all business logic. The server OS and PostgreSQL are configured in UTC; IST is applied in application logic for all date comparisons (sandwich rule, holiday check, year-end lapse, escalation timing).
Confidence: HIGH
Risk if wrong: Hangfire scheduled jobs (Dec 31 lapse at 23:59 IST, Jan 1 credit at 00:01 IST, daily escalation at 08:00 IST) run at the wrong time
Verification: Confirm server timezone policy with DevOps; confirm Hangfire cron expressions will be authored in IST-offset UTC equivalents
Status: CONFIRMED

---

ASSUMPTION-8:
Statement: The team overlap limit default of 2 employees per department per date is acceptable for all departments without per-department override in Phase 1. Individual departments can adjust their limit via the Department edit screen.
Confidence: HIGH
Risk if wrong: Departments with different natural team sizes may find the default too restrictive or too permissive
Verification: Confirm with HR that configurable per-department limit (DEPT-01, DEPT-08) meets all departmental needs
Status: CONFIRMED

---

ASSUMPTION-9:
Statement: React 17 (not React 18) is acceptable for Phase 1. No concurrent rendering features from React 18 are required.
Confidence: HIGH
Risk if wrong: Some MUI v5 components may have React 18 deprecation warnings in future; upgrade path is clear
Verification: Confirm with frontend team that React 17 is the mandated version
Status: CONFIRMED

---

ASSUMPTION-10:
Statement: The Google Calendar integration requires per-user OAuth2 consent on first use. Users must individually authorize the LMS to access their Google Calendar. There is no service account / domain-wide delegation approach.
Confidence: MEDIUM
Risk if wrong: If the organization uses Google Workspace domain-wide delegation, the per-user consent flow is unnecessary and adds friction
Verification: Confirm with Google Workspace admin whether domain-wide delegation is preferred or available
Status: CONFIRMED
