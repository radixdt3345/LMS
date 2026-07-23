# F-02 — Account Lockout Management

## Purpose
Enforce account security by locking accounts after 3 consecutive failed local login attempts, and provide HR Admin / Super Admin with a dedicated screen to view and unlock locked accounts.

## User Stories

### US-02.1: View Locked Accounts
As an HR Admin or Super Admin, I want to see all currently locked accounts so that I can identify users who need to be unlocked.

**Acceptance Criteria:**
- AC-8: GET /api/v1/accounts/locked by HR Admin returns HTTP 200 with list of locked accounts.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| HR Admin | View locked accounts list | — |
| Super Admin | View locked accounts list | — |
| Employee / Manager | — | Access this endpoint (403) |

### US-02.2: Unlock a Locked Account
As an HR Admin or Super Admin, I want to unlock a specific locked account so that the user can regain access.

**Acceptance Criteria:**
- AC-9: POST /api/v1/accounts/{id}/unlock by HR Admin returns HTTP 200; subsequent login with that account succeeds.

**RBAC:**
| Role | Can | Cannot |
|------|-----|--------|
| HR Admin | Unlock any account | — |
| Super Admin | Unlock any account | — |
| Employee / Manager | — | Unlock accounts (403) |

## Functional Requirements Covered
| FR-ID | Requirement | Priority |
|-------|-------------|----------|
| FR-7 | Account locks after 3 consecutive failed logins | MUST |
| FR-8 | Locked accounts unlockable by HR Admin or Super Admin | MUST |

## Playwright Scenarios
| PT-ID | Scenario | Roles Involved |
|-------|----------|---------------|
| PT-2 | 3 failed logins → account locked → HR Admin unlocks | HR Admin |

## Entity Ownership
| Entity | Read | Write | Delete |
|--------|------|-------|--------|
| users (failed_login_attempts, locked_at) | AccountService | AccountService | — |
| audit_logs | — | AuditService (lock/unlock events) | — |

## Integration Points
None (purely internal)

## HITL Flag
NO

## Execution Wave
Wave 1: Foundation — depends on F-01 (auth); required before F-07 (Employee Management) to correctly test lock behavior.

## Dependencies
Depends on: F-01
Blocks: NONE
