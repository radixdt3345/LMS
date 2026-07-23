# F-01 — User Authentication

## Purpose
Provide secure entry into the LMS via two pathways: Azure AD SSO (OAuth2 Authorization Code Flow) for corporate accounts, and local email/password login as a fallback. Issues JWTs with role/department claims, maintains refresh tokens, and enforces account lockout after repeated failures.

## User Stories

### US-01.1: SSO Login via Azure AD
As an Employee, I want to click "Sign in with Microsoft" so that I am authenticated via my corporate Azure AD account without a separate LMS password.

**Acceptance Criteria:**
- AC-1: GET /api/v1/auth/sso/login returns HTTP 302 redirect to Azure AD authorization endpoint.
- AC-2: GET /api/v1/auth/sso/callback with valid authorization code returns HTTP 200 with access_token and sets HttpOnly refresh token cookie.
- AC-10: First SSO login by unmapped AD group creates Employee account with status Active.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| Unauthenticated | Initiate SSO flow; complete callback | Access any protected route |
| All authenticated | Receive JWT after successful SSO | — |

### US-01.2: Local Email/Password Login
As an Employee, I want to log in with my email and password so that I can access the system without a Microsoft account.

**Acceptance Criteria:**
- AC-3: POST /api/v1/auth/login with valid credentials returns HTTP 200 with access_token and HttpOnly refresh token cookie.
- AC-4: POST /api/v1/auth/login with invalid credentials returns HTTP 401.
- AC-5: Returned JWT decodes to contain user_id, role, department_id, exp.
- AC-6: Password "abc123" (no uppercase) returns HTTP 422.
- AC-7: After 3 consecutive failed attempts, 4th returns HTTP 423.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| Unauthenticated | Submit local login | Access any protected route |

### US-01.3: Token Refresh and Logout
As an Employee, I want my session to remain active across page refreshes and to be able to log out so that my session is secure.

**Acceptance Criteria:**
- POST /api/v1/auth/refresh with valid HttpOnly cookie → 200 + new access_token (IT-AUTH-004)
- POST /api/v1/auth/logout invalidates refresh token in DB (IT-AUTH-005)
- FR-10: Role changes take effect on next token refresh, not mid-session.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| Authenticated | Refresh token; logout | — |

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-1 | Azure AD SSO via OAuth2 Authorization Code Flow | MUST |
| FR-2 | AD group mapping to LMS roles via app config | MUST |
| FR-3 | Local email/password fallback login | MUST |
| FR-4 | JWT (24h) + refresh token (7d, DB-stored) on login | MUST |
| FR-5 | Refresh token invalidated on logout | MUST |
| FR-6 | Password: min 8 chars, 1 uppercase, 1 number | MUST |
| FR-9 | JWT contains user_id, role, department_id | MUST |
| FR-10 | Role changes take effect on next token refresh | MUST |
| FR-11 | Unmapped SSO user → Employee role by default | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| PT-1 | Local login → Employee Dashboard visible | Employee |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| users (auth fields) | AuthService | AuthService | — |
| refresh_tokens | AuthService | AuthService (create/invalidate) | AuthService (on logout) |

## Integration Points
- Azure AD OAuth2 (MSAL) — SSO flow
- JWT generation (in-process)

## HITL Flag
NO

## Execution Wave
Wave 1: Foundation — all other features depend on authentication. Must be the first domain implemented.

## Dependencies
Depends on: NONE
Blocks: F-02, F-03, F-04, F-05, F-06, F-07, F-08, F-09, F-10, F-11, F-12, F-13, F-14
