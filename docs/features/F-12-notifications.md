# F-12 — Notifications

## Purpose
Deliver email notifications via SendGrid v3 (plain text / inline HTML — no dynamic templates) and maintain an in-app notification center with read/unread tracking. Google Calendar sync is included here as a notification side-effect of leave approval. Notification delivery failures are retried via Hangfire; permanent failures are logged without reversing the triggering action.

## User Stories

### US-12.1: Email Notifications
As an Employee or Manager, I want to receive email notifications for every leave/comp-off status change so that I am always informed without having to check the app.

**Acceptance Criteria:**
- AC-52: Email sent to reporting manager (or HR Admin) within 60 seconds of submission.
- AC-53: SendGrid 5xx → Hangfire retry queued; after 5 retries → notification.email_status = "email delivery failed".
- FR-66: Email notifications for: leave applied, approved, rejected, cancelled, revoked, escalation reminder, comp-off applied/approved/rejected.
- FR-67: Hangfire retry with 5 attempts over 24 hours on SendGrid failure.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| All authenticated | Receive email notifications | — |

### US-12.2: In-App Notification Center
As an Employee, I want to see a notification center with read/unread status and navigate to the related leave request so that I have a consolidated view of all activity.

**Acceptance Criteria:**
- AC-54: GET /api/v1/notifications → 200 with notifications (read, title, message, related_entity_type, related_entity_id).
- FR-68: Click notification → navigate to related leave/comp-off request.
- FR-71: Unread count polled every 60 seconds.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| All authenticated | View own notifications; mark as read | View others' notifications |

### US-12.3: Google Calendar Sync
As an Employee, I want approved leaves synced as all-day events to my Google Calendar so that my calendar reflects my planned absences automatically.

**Acceptance Criteria:**
- AC-55: On leave approval, CalendarSyncJob creates all-day event in employee's Google Calendar.
- AC-56: 3 retry failures → notification.calendar_status = "calendar sync failed"; leave status remains Approved.
- FR-69: Hangfire job with 3 retries (exponential backoff) for calendar create/delete.
- FR-70: Calendar failure ≠ approval reversal.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| Employee / Manager | Trigger sync (implicit via leave approval) | — |

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-66 | Email via SendGrid for all leave/comp-off events | MUST |
| FR-67 | Hangfire retry on SendGrid failure (5x over 24h) | MUST |
| FR-68 | In-app notification center with read/unread | MUST |
| FR-69 | Google Calendar sync on approval/cancel/revoke | MUST |
| FR-70 | Calendar sync failure logged; approval not reversed | MUST |
| FR-71 | Unread count polled every 60 seconds | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| PT-11 | Leave approval triggers notifications + Google Calendar event | Employee, Manager |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| notifications | Owner (own) | NotificationService | — |
| Hangfire jobs | — | EmailService, CalendarSyncService | — |

## Integration Points
- SendGrid v3 REST API (plain text / inline HTML; no dynamic templates)
- Google Calendar API v3 (company-wide service account — single service account JSON key stored as secret)
- Hangfire: EmailJob (5 retries), CalendarSyncJob (3 retries, exponential backoff), EscalationJob

## HITL Flag
NO — **RESOLVED (HIL 3):** Google Calendar integration uses a **company-wide service account** (not per-user OAuth2). The LMS holds a single Google service account credential that writes to a shared company calendar. No per-employee consent or per-user token management is required. CalendarSyncJob creates/deletes events on behalf of the company. All OAuth2 credential complexity is eliminated — only the service account JSON key needs to be stored securely (as an environment variable/secret).

## Execution Wave
Wave 2: Core — depends on leave requests and approval engine existing (F-09, F-11).

## Dependencies
Depends on: F-09 (Leave Requests), F-10 (Comp-off), F-11 (Approval Engine)
Blocks: F-13 (Dashboards — notification center widget), F-14 (Audit Trail — notification events)
