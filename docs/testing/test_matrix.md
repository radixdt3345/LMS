# Test Matrix — Leave Management System (LMS)

Story × Test Layer matrix. ✓ = required | – = not applicable

| User Story | L1 Unit | L2 Integration | L3 Contract | L4 API Functional | L5 Perf | L6 Security | L7 Smoke | L8 UAT | L9 Regression |
|-----------|---------|---------------|------------|------------------|---------|------------|---------|--------|--------------|
| **US-1** Login (SSO + local) | ✓ | ✓ | ✓ | ✓ | – | ✓ | ✓ | ✓ | ✓ |
| **US-2** Account lock + HR unlock | ✓ | ✓ | ✓ | ✓ | – | ✓ | – | ✓ | ✓ |
| **US-3** HR Admin creates employee | ✓ | ✓ | ✓ | ✓ | – | – | – | ✓ | ✓ |
| **US-4** Employee views/edits own profile | ✓ | – | ✓ | ✓ | – | – | – | ✓ | ✓ |
| **US-5** HR Admin manages departments | ✓ | ✓ | ✓ | ✓ | – | – | – | ✓ | ✓ |
| **US-6** Employee views leave balances | ✓ | ✓ | ✓ | ✓ | ✓ | – | ✓ | ✓ | ✓ |
| **US-7** Employee applies for leave (full validation) | ✓ | ✓ | ✓ | ✓ | ✓ | – | ✓ | ✓ | ✓ |
| **US-8** Cancel leave / HR revokes leave | ✓ | ✓ | ✓ | ✓ | – | – | – | ✓ | ✓ |
| **US-9** Comp-off request + manager approval | ✓ | ✓ | ✓ | ✓ | – | – | – | ✓ | ✓ |
| **US-10** L1/L2 approval + escalation | ✓ | ✓ | ✓ | ✓ | ✓ | – | ✓ | ✓ | ✓ |
| **US-11** HR Admin manages holidays (+ CSV) | ✓ | ✓ | ✓ | ✓ | – | – | – | ✓ | ✓ |
| **US-12** Notifications (email + in-app + calendar) | ✓ | ✓ | – | ✓ | – | – | ✓ | ✓ | ✓ |
| **US-13** Role-appropriate dashboards + CSV export | ✓ | ✓ | ✓ | ✓ | ✓ | – | ✓ | ✓ | ✓ |
| **US-14** Audit trail search | ✓ | ✓ | ✓ | ✓ | – | ✓ | – | ✓ | ✓ |
| **US-15** Initial seed (idempotent) | ✓ | ✓ | – | ✓ | – | – | – | ✓ | ✓ |

---

## Critical-Path Test IDs by Story

### US-7 — Leave Application (highest complexity)

| Test ID | Layer | Scenario |
|---------|-------|---------|
| UT-LEAVE-001 | L1 | SandwichRuleEngine: isolated holiday between two leave days → not counted |
| UT-LEAVE-002 | L1 | SandwichRuleEngine: chained Thu holiday + Fri leave + Sat/Sun weekend → 4 days |
| UT-LEAVE-003 | L1 | SandwichRuleEngine: leave + weekend + leave (two separate requests) → weekend not counted |
| UT-LEAVE-004 | L1 | LeaveBalanceService: sufficient balance → allows deduction |
| UT-LEAVE-005 | L1 | LeaveBalanceService: zero balance → throws InsufficientBalanceException |
| UT-LEAVE-006 | L1 | LeaveBalanceService: Unpaid Leave → always allows (exempt from balance check) |
| UT-LEAVE-007 | L1 | Half-day conflict: AM exists → PM submission blocked with correct error |
| IT-LEAVE-001 | L2 | Full submission pipeline: valid request → status = PendingL1, audit log written |
| IT-LEAVE-002 | L2 | Weekend start date → 422 with LEAVE_WEEKEND_OR_HOLIDAY |
| IT-LEAVE-003 | L2 | Holiday start date → 422 |
| IT-LEAVE-004 | L2 | Insufficient balance → 422 with LEAVE_INSUFFICIENT_BALANCE |
| IT-LEAVE-005 | L2 | Team overlap limit reached → 422 with LEAVE_TEAM_OVERLAP_LIMIT |
| IT-LEAVE-006 | L2 | Overlapping approved leave → 422 with LEAVE_OVERLAP_CONFLICT |
| IT-LEAVE-007 | L2 | days_count computed correctly with sandwich rule (Thu holiday + Fri leave + weekend = 4) |
| IT-LEAVE-008 | L2 | Retroactive request → requires_l2 = true |
| API-LEAVE-001 | L4 | Employee role → 201 on valid submit |
| API-LEAVE-002 | L4 | HR Admin role → 403 on submit |
| API-LEAVE-003 | L4 | Manager role → 201 on valid submit |

### US-10 — Approval Engine

| Test ID | Layer | Scenario |
|---------|-------|---------|
| UT-APR-001 | L1 | No manager → L1 approver = HR Admin |
| UT-APR-002 | L1 | HR Admin as L1 → L2 skipped (status = Approved directly) |
| UT-APR-003 | L1 | Duration > 3 days → L2 required after L1 |
| UT-APR-004 | L1 | RequiresHRFlag = true → L2 required after L1 |
| UT-APR-005 | L1 | Retroactive → L2 required after L1 |
| IT-APR-001 | L2 | L1 approve → status = L1Approved; L2 condition checked |
| IT-APR-002 | L2 | L2 approve → status = Approved; balance deducted |
| IT-APR-003 | L2 | L1 reject → status = Rejected; balance NOT deducted; email enqueued |
| IT-APR-004 | L2 | EscalationJob: pending > 2 days → email enqueued for approver |
| API-APR-001 | L4 | Manager approves for non-direct-report → 403 |
| API-APR-002 | L4 | HR Admin attempts L2 before L1 → 422 |

### US-1 — Authentication (100% coverage required)

| Test ID | Layer | Scenario |
|---------|-------|---------|
| UT-AUTH-001 | L1 | Valid password → JWT contains user_id, role, department_id, exp |
| UT-AUTH-002 | L1 | Wrong password → failed_login_attempts incremented |
| UT-AUTH-003 | L1 | 3 failed attempts → account locked |
| UT-AUTH-004 | L1 | Refresh token valid → new access token issued |
| UT-AUTH-005 | L1 | Refresh token expired → 401 |
| IT-AUTH-001 | L2 | POST /auth/login valid → 200 + token + HttpOnly cookie |
| IT-AUTH-002 | L2 | POST /auth/login invalid → 401 |
| IT-AUTH-003 | L2 | POST /auth/login 3x invalid → 423 on 4th attempt |
| IT-AUTH-004 | L2 | POST /auth/refresh valid cookie → 200 + new token |
| IT-AUTH-005 | L2 | POST /auth/logout → refresh token invalidated in DB |
| IT-AUTH-006 | L2 | SSO callback with valid code → 200 + token (mock Azure AD) |
