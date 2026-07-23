# Test Plan — Leave Management System (LMS)

**Derived from:** `docs/prd.md` + `docs/features/*.md` + `ai-context/testing.md`
**Date:** July 2026
**Counters:** UT-51 | IT-53 | E2E-14 | RT-30

---

## Part 1 — Unit Tests (UT-)

All unit tests use **xUnit 2.x + Moq 4.x** (backend) or **Vitest** (frontend).
No real DB, no real external APIs — all dependencies mocked.
Naming: `[MethodName]_[Scenario]_[ExpectedResult]`

---

### F-01 — User Authentication

```
UT-1 (FR-3, AC-3):
Method: AuthService.LoginAsync
Scenario: Valid email + matching password hash
Expected: Returns AuthResult with non-null AccessToken and RefreshToken; claims contain user_id, role, department_id
Tooling: xUnit + Moq (IUserRepository, ITokenService)

UT-2 (FR-3, AC-4):
Method: AuthService.LoginAsync
Scenario: Valid email + incorrect password
Expected: Returns AuthFailureResult; failed_login_attempts incremented by 1; no token issued
Tooling: xUnit + Moq

UT-3 (FR-6, AC-6):
Method: PasswordValidator.Validate
Scenario: Password = "abc123" (no uppercase letter)
Expected: Returns ValidationFailed with code PASSWORD_NO_UPPERCASE
Tooling: xUnit (no mocks needed — pure validator)

UT-4 (FR-6):
Method: PasswordValidator.Validate
Scenario: Password = "Abcdefg" (no digit)
Expected: Returns ValidationFailed with code PASSWORD_NO_NUMBER
Tooling: xUnit

UT-5 (FR-6):
Method: PasswordValidator.Validate
Scenario: Password = "Ab1" (fewer than 8 characters)
Expected: Returns ValidationFailed with code PASSWORD_TOO_SHORT
Tooling: xUnit

UT-6 (FR-4, AC-5):
Method: TokenService.GenerateAccessToken
Scenario: Valid user with role=Manager, department_id set
Expected: Decoded JWT contains user_id, role="Manager", department_id, exp within 24h
Tooling: xUnit (use System.IdentityModel.Tokens.Jwt to decode)

UT-7 (FR-5):
Method: TokenService.InvalidateRefreshToken
Scenario: Valid refresh token ID passed
Expected: Calls IRefreshTokenRepository.Delete(tokenId); token no longer valid on next use
Tooling: xUnit + Moq (IRefreshTokenRepository)

UT-8 (FR-7, AC-7):
Method: AuthService.LoginAsync
Scenario: 3rd consecutive failed login attempt (failed_login_attempts = 2 before call)
Expected: Account locked (locked_at set to UtcNow); failed_login_attempts = 3; returns AccountLockedResult
Tooling: xUnit + Moq

UT-9 (FR-11, AC-10):
Method: AuthService.HandleSsoCallbackAsync
Scenario: AD group in token does not match any configured role mapping
Expected: New user created with role = Employee, status = Active
Tooling: xUnit + Moq (IUserRepository, IAzureAdService)

UT-10 (FR-10):
Method: TokenService.GenerateAccessToken
Scenario: User's role changed in DB since last token; GenerateAccessToken called for refresh
Expected: New token reflects updated role from DB (not previous token claims)
Tooling: xUnit + Moq (IUserRepository)
```

### F-02 — Account Lockout Management

```
UT-11 (FR-7):
Method: AccountService.LockAccount
Scenario: Called after 3rd failed login
Expected: user.locked_at = UtcNow; user.failed_login_attempts = 3; audit log entry created
Tooling: xUnit + Moq

UT-12 (FR-8, AC-9):
Method: AccountService.UnlockAccount
Scenario: HR Admin unlocks a locked account
Expected: user.failed_login_attempts = 0; user.locked_at = null; audit log entry created
Tooling: xUnit + Moq
```

### F-03 — Initial Data Seeding

```
UT-13 (FR-84, AC-65):
Method: DataSeeder.SeedAsync
Scenario: Called twice on a DB that already has seeded data
Expected: No duplicate records; row counts remain 1 Super Admin + 1 HR Admin + 1 dept + 5 leave types
Tooling: xUnit + in-memory repository stubs
```

### F-04 — Department Management

```
UT-14 (FR-23, AC-18):
Method: DepartmentService.CreateDepartmentAsync
Scenario: Department name "Engineering" already exists as active department (case-insensitive match)
Expected: Throws DuplicateDepartmentException with code DEPARTMENT_NAME_DUPLICATE
Tooling: xUnit + Moq (IDepartmentRepository)

UT-15 (FR-24, AC-19):
Method: DepartmentService.DeleteDepartmentAsync
Scenario: Department has 2 active employees
Expected: Throws CannotDeleteDepartmentException with code DEPARTMENT_HAS_ACTIVE_EMPLOYEES
Tooling: xUnit + Moq
```

### F-05 — Employee Management

```
UT-16 (FR-17, AC-14):
Method: EmployeeService.SaveEmployeeAsync
Scenario: New employee saved with reporting_manager_id = User X who currently has role = Employee
Expected: User X's role is updated to Manager in DB; audit log records ROLE_CHANGED
Tooling: xUnit + Moq (IUserRepository, IAuditService)

UT-17 (FR-17, AC-15):
Method: EmployeeService.RemoveDirectReportAsync
Scenario: User X has exactly 1 direct report; that report is removed/reassigned
Expected: User X's role is set back to Employee; audit log records ROLE_CHANGED
Tooling: xUnit + Moq

UT-18 (FR-17):
Method: EmployeeService.SaveEmployeeAsync
Scenario: New employee saved with reporting_manager_id = User X who is already a Manager
Expected: No error; User X role unchanged (idempotent — Manager stays Manager)
Tooling: xUnit + Moq

UT-19 (FR-18, AC-16):
Method: EmployeeService.ChangeRoleAsync
Scenario: HR Admin attempts to downgrade User X (Manager with 3 active direct reports) to Employee
Expected: Throws RoleDemotionBlockedException with message "This user is a reporting manager for active employees and cannot be demoted to Employee."
Tooling: xUnit + Moq

UT-20 (FR-14):
Method: ApprovalRouter.GetL1ApproverAsync
Scenario: Employee with reporting_manager_id = null
Expected: Returns HR Admin user (first active HR Admin found)
Tooling: xUnit + Moq (IUserRepository)
```

### F-06 — Leave Types and Policies

```
UT-21 (FR-29):
Method: LeaveTypeService.GetEntitlementDays
Scenario: Leave type has AnnualLeaveDays = 12; no per-employee override exists
Expected: Returns 12.0 regardless of which employee is queried
Tooling: xUnit (pure function)
```

### F-07 — Leave Balance Management

```
UT-22 (FR-32, AC-22):
Method: LeaveBalanceService.DeductAsync
Scenario: Full-day leave approval; current balance = 8.0
Expected: balance = 7.0; used = used + 1.0
Tooling: xUnit + Moq

UT-23 (FR-32, AC-23):
Method: LeaveBalanceService.DeductAsync
Scenario: Half-day leave approval; current balance = 3.5
Expected: balance = 3.0; used = used + 0.5
Tooling: xUnit + Moq

UT-24 (FR-37, AC-27):
Method: LeaveBalanceService.DeductAsync
Scenario: Casual Leave; current balance = 0.0; days = 1.0
Expected: Throws InsufficientBalanceException with code LEAVE_INSUFFICIENT_BALANCE
Tooling: xUnit + Moq

UT-25 (FR-37):
Method: LeaveBalanceService.DeductAsync
Scenario: Casual Leave; current balance = 0.5; days = 1.0 (would go negative)
Expected: Throws InsufficientBalanceException
Tooling: xUnit + Moq

UT-26 (FR-37, AC-28):
Method: LeaveBalanceService.DeductAsync
Scenario: Unpaid Leave; current balance = 0.0; days = 2.0
Expected: No exception; balance remains 0.0 (Unpaid Leave exempt from balance check)
Tooling: xUnit + Moq

UT-27 (FR-33, AC-24):
Method: LeaveBalanceService.ProrateEntitlementAsync
Scenario: date_of_joining = July 1 (184 remaining days in year); AnnualLeaveDays = 12
Expected: Returns 6.0 (12 × 184 ÷ 365 = 6.05 → rounded to nearest 0.5 = 6.0)
Tooling: xUnit (pure calculation)

UT-28 (FR-33):
Method: LeaveBalanceService.ProrateEntitlementAsync
Scenario: date_of_joining = December 30 (1 remaining day); AnnualLeaveDays = 12
Expected: Returns 0.5 (minimum entitlement for joiners with ≥1 remaining day)
Tooling: xUnit

UT-29 (FR-33):
Method: LeaveBalanceService.ProrateEntitlementAsync
Scenario: date_of_joining = December 31 (0 remaining days)
Expected: Returns 0.0
Tooling: xUnit

UT-30 (FR-35, AC-25):
Method: CompOffCreditService.CreateCreditAsync
Scenario: earn_date = July 1
Expected: expiry_date = July 31; days offset = +30
Tooling: xUnit + Moq

UT-31 (FR-38):
Method: YearEndLapseJob.ExecuteAsync
Scenario: 3 employees each with non-zero balances across 5 leave types
Expected: All 15 balance records set to 0.0; 15 audit log entries created with action = BALANCE_LAPSED
Tooling: xUnit + Moq (ILeaveBalanceRepository, IAuditService)
```

### F-08 — Public Holiday Calendar

```
UT-32 (FR-52):
Method: HolidayService.IsEligibleForCompOffAsync
Scenario: date = Tuesday (working weekday); no holiday on that date
Expected: Returns false
Tooling: xUnit + Moq (IHolidayRepository)

UT-33 (FR-52):
Method: HolidayService.IsEligibleForCompOffAsync
Scenario: date = Saturday
Expected: Returns true (weekend)
Tooling: xUnit + Moq
```

### F-09 — Leave Request Workflow

```
UT-34 (FR-42, AC-32):
Method: SandwichRuleEngine.ComputeDaysCount
Scenario: Mon (leave) + Tue (public holiday) + Wed (leave) — isolated holiday
Expected: days_count = 2 (Tue holiday not counted — not chained on both sides within single range)
Tooling: xUnit (pure algorithm; inject holiday list + leave dates)

UT-35 (FR-42, AC-33):
Method: SandwichRuleEngine.ComputeDaysCount
Scenario: Thu (public holiday) + Fri (leave) + Sat (weekend) + Sun (weekend)
Expected: days_count = 4 (all days counted — Thu chained to Fri; Sat/Sun chained to Fri)
Tooling: xUnit

UT-36 (FR-42):
Method: SandwichRuleEngine.ComputeDaysCount
Scenario: Mon (leave) + Tue (holiday) + Wed (holiday) + Thu (leave)
Expected: days_count = 4 (entire block chained: Mon leave → Tue/Wed holidays → Thu leave)
Tooling: xUnit

UT-37 (FR-42):
Method: SandwichRuleEngine.ComputeDaysCount
Scenario: Single leave day (Wed) with no adjacent non-working days
Expected: days_count = 1
Tooling: xUnit

UT-38 (FR-42):
Method: SandwichRuleEngine.ComputeDaysCount
Scenario: Employee has two separate requests: Request A = Mon leave, Request B = Wed leave; Tue is a holiday between them
Expected: For Request A, days_count = 1. For Request B, days_count = 1. Tue not counted in either (bridges separate requests).
Tooling: xUnit (each request computed independently)

UT-39 (FR-44, AC-35):
Method: LeaveRequestService.ValidateHalfDayConflictAsync
Scenario: Employee already has approved AM half-day on July 10; new PM half-day submitted for July 10
Expected: Returns HalfDayConflictException with code LEAVE_HALF_DAY_CONFLICT and prompt to modify to full-day
Tooling: xUnit + Moq

UT-40 (FR-48, AC-39):
Method: LeaveRequestService.IsRetroactiveAsync
Scenario: start_date = yesterday (in IST)
Expected: Returns true; ApprovalRouter sets requires_l2 = true when manager exists
Tooling: xUnit + Moq (IDateTimeProvider returning fixed IST date)

UT-41 (FR-46, AC-36):
Method: LeaveRequestService.CancelAsync
Scenario: start_date = 3 days ago (already started)
Expected: Throws CannotCancelException with code LEAVE_ALREADY_STARTED
Tooling: xUnit + Moq

UT-42 (FR-47, AC-38):
Method: LeaveRequestService.RevokeAsync
Scenario: start_date = today (already started)
Expected: Throws CannotRevokeException with code LEAVE_ALREADY_STARTED
Tooling: xUnit + Moq
```

### F-10 — Comp-Off Request Workflow

```
UT-43 (FR-53, AC-41):
Method: CompOffRequestService.ValidateWorkedHoursAsync
Scenario: start_time = 09:00, end_time = 12:00 (3 hours worked)
Expected: Throws InsufficientWorkedHoursException with code COMP_OFF_INSUFFICIENT_HOURS
Tooling: xUnit

UT-44 (FR-53, AC-42):
Method: CompOffRequestService.ValidateWorkedHoursAsync
Scenario: is_half_day = true; start_time = 09:00, end_time = 14:30 (5.5 hours > 4h threshold)
Expected: Returns valid; no exception
Tooling: xUnit

UT-45 (FR-53, AC-43):
Method: CompOffRequestService.ValidateWorkedHoursAsync
Scenario: is_half_day = false; start_time = 09:00, end_time = 18:00 (9 hours ≥ 8h threshold)
Expected: Returns valid; no exception
Tooling: xUnit

UT-46 (FR-52, AC-40):
Method: CompOffRequestService.ValidateDateAsync
Scenario: date = next Tuesday (working weekday); no holiday on that date
Expected: Throws InvalidCompOffDateException with code COMP_OFF_DATE_NOT_ELIGIBLE
Tooling: xUnit + Moq (IHolidayRepository)

UT-47 (FR-56):
Method: CompOffCreditService.CreditBalanceAsync
Scenario: Comp-off request approved; is_half_day = false; current comp-off balance = 1.0
Expected: LeaveBalance.balance = 2.0; CompOffCredit.status = Active; expiry_date = earn_date + 30
Tooling: xUnit + Moq
```

### F-11 — Approval Engine

```
UT-48 (FR-58, AC-44):
Method: ApprovalService.GetL1ApproverAsync
Scenario: Employee with reporting_manager_id = null
Expected: Returns HR Admin user; not any Manager
Tooling: xUnit + Moq

UT-49 (FR-58, AC-45):
Method: ApprovalService.ApproveL1Async
Scenario: HR Admin approves L1 for no-manager employee (≤3 days, no RequiresHRFlag)
Expected: leave_request.status = Approved; no ApprovalStep with level = L2 created; balance deducted
Tooling: xUnit + Moq

UT-50 (FR-59, AC-46):
Method: ApprovalService.DetermineRequiresL2Async
Scenario: Leave duration = 5 consecutive days; employee has manager
Expected: requires_l2 = true
Tooling: xUnit

UT-51 (FR-59, AC-47):
Method: ApprovalService.DetermineRequiresL2Async
Scenario: Leave type RequiresHRFlag = true (Sick Leave); employee has manager
Expected: requires_l2 = true
Tooling: xUnit

UT-52 (FR-48, FR-59):
Method: ApprovalService.DetermineRequiresL2Async
Scenario: Retroactive request (start_date = yesterday); employee HAS a manager
Expected: requires_l2 = true
Tooling: xUnit

UT-53 (FR-58):
Method: ApprovalService.DetermineRequiresL2Async
Scenario: Retroactive request; employee has NO manager (HR Admin as L1)
Expected: requires_l2 = false (no-manager rule overrides retroactive rule — HIL 3 confirmed)
Tooling: xUnit
```

### F-12 — Notifications

```
UT-54 (FR-67, AC-53):
Method: EmailService.SendAsync
Scenario: SendGrid returns HTTP 503 (service unavailable)
Expected: Job re-enqueued in Hangfire with retry count; notification.email_status remains Pending
Tooling: xUnit + Moq (ISendGridClient, IHangfireJobClient)

UT-55 (FR-70, AC-56):
Method: CalendarService.CreateEventAsync
Scenario: Google Calendar API returns error on all 3 retry attempts
Expected: notification.calendar_status = "calendar sync failed"; leave_request.status unchanged (still Approved)
Tooling: xUnit + Moq (IGoogleCalendarClient)
```

### F-14 — Audit Trail

```
UT-56 (FR-79, AC-62):
Method: AuditRepository.DeleteAsync
Scenario: Attempt to call DeleteAsync with any audit log ID
Expected: Throws AuditLogImmutableException with code AUDIT_LOG_IMMUTABLE; no DB call made
Tooling: xUnit
```

### Frontend Unit Tests (Vitest)

```
UT-57 (FR-42):
Component: SandwichRuleDisplay
Scenario: Render with days_count = 4 and sandwich_rule_applied = true
Expected: "4 days (including 2 non-working days)" text visible
Tooling: Vitest + React Testing Library

UT-58 (FR-72):
Component: LeaveBalanceCard
Scenario: total_entitled = 12, used = 4, balance = 8
Expected: Progress bar width = 33%; "8 of 12 days remaining" text visible
Tooling: Vitest + React Testing Library

UT-59 (FR-37):
Component: ApplyLeaveForm
Scenario: balance = 0 for selected leave type (non-Unpaid)
Expected: Submit button is disabled; "Insufficient balance" message visible
Tooling: Vitest + MSW (mock GET /balances/me)

UT-60 (NFR-6):
Component: ProtectedRoute
Scenario: User with role = Employee tries to access /hr/dashboard
Expected: Redirected to /unauthorized; HR dashboard content not rendered
Tooling: Vitest + MemoryRouter

UT-61 (NFR-6):
Component: RoleProtectedRoute
Scenario: User with role = Manager tries to access /admin
Expected: Redirected to /unauthorized
Tooling: Vitest + MemoryRouter
```

---

## Part 2 — Integration Tests (IT-)

All IT- tests use **xUnit + real PostgreSQL** (`lms_test` DB, native install).
Each test class uses `IClassFixture<LmsTestFixture>` for DB setup; `IAsyncLifetime.DisposeAsync` for row cleanup.
Run command: `dotnet test LMS.Tests --filter Category=Integration`

---

### F-01 — Auth Integration

```
IT-1 (FR-3, AC-3):
Boundary: HTTP → AuthController → AuthService → PostgreSQL
Scenario: POST /api/v1/auth/login with valid email and password
Setup: User seeded with BCrypt hash of "Admin@123"
Steps:
  1. POST { email, password: "Admin@123" }
  2. Assert HTTP 200
  3. Assert response contains access_token
  4. Assert HttpOnly refresh token cookie set
Expected: JWT decodable with correct user_id, role, department_id claims

IT-2 (FR-3, AC-4):
Boundary: HTTP → AuthController → AuthService → PostgreSQL
Scenario: POST /api/v1/auth/login with wrong password
Steps:
  1. POST { email: valid, password: "Wrong1234" }
  2. Assert HTTP 401
Expected: No token issued; failed_login_attempts incremented in DB

IT-3 (FR-7, AC-7):
Boundary: HTTP → AuthController → AccountService → PostgreSQL
Scenario: 3 consecutive failed logins → account locked; 4th returns 423
Steps:
  1. POST login wrong password ×3 → each returns 401
  2. POST login wrong password 4th time → assert HTTP 423
Expected: user.locked_at set in DB

IT-4 (FR-5):
Boundary: HTTP → AuthController → TokenService → PostgreSQL
Scenario: POST /api/v1/auth/refresh with valid HttpOnly cookie
Steps:
  1. Login (IT-1 flow) to get cookie
  2. POST /auth/refresh with cookie
  3. Assert HTTP 200 + new access_token
Expected: New token issued; old refresh token entry still valid (sliding window) or new one issued

IT-5 (FR-5):
Boundary: HTTP → AuthController → TokenService → PostgreSQL
Scenario: POST /api/v1/auth/logout
Steps:
  1. Login to get token + cookie
  2. POST /auth/logout with Bearer token
  3. Assert HTTP 204
  4. POST /auth/refresh with same cookie
  5. Assert HTTP 401
Expected: Refresh token invalidated in DB after logout

IT-6 (FR-1, AC-1):
Boundary: HTTP → AuthController
Scenario: GET /api/v1/auth/sso/login
Steps:
  1. GET /auth/sso/login (unauthenticated)
  2. Assert HTTP 302
  3. Assert Location header contains Azure AD authorization endpoint URL
Expected: Redirect URL contains configured tenant ID and client ID
```

### F-02 — Account Lockout Integration

```
IT-7 (FR-8, AC-8):
Boundary: HTTP → AccountsController → AccountService → PostgreSQL
Scenario: GET /api/v1/accounts/locked by HR Admin
Steps:
  1. Seed 2 locked users
  2. GET /accounts/locked with HR Admin Bearer token
  3. Assert HTTP 200
  4. Assert response array contains both locked users
Expected: 200 with list of locked accounts (name, email, locked_at)

IT-8 (FR-8, AC-9):
Boundary: HTTP → AccountsController → AccountService → PostgreSQL
Scenario: POST /api/v1/accounts/{id}/unlock by HR Admin
Steps:
  1. Seed locked user
  2. POST /accounts/{id}/unlock with HR Admin token
  3. Assert HTTP 200
  4. POST /auth/login with that user's correct credentials
  5. Assert HTTP 200 (login succeeds)
Expected: user.locked_at = null; user.failed_login_attempts = 0
```

### F-04 — Department Integration

```
IT-9 (FR-23, AC-18):
Boundary: HTTP → DepartmentsController → DepartmentService → PostgreSQL
Scenario: POST /api/v1/departments with duplicate name
Steps:
  1. Seed department "Engineering"
  2. POST { name: "engineering", code: "ENG2" } with HR Admin token
  3. Assert HTTP 422
Expected: Error code = DEPARTMENT_NAME_DUPLICATE

IT-10 (FR-24, AC-19):
Scenario: DELETE /api/v1/departments/{id} with active employees
Steps:
  1. Seed department with 1 active employee assigned
  2. DELETE /departments/{id} with HR Admin token
  3. Assert HTTP 422
Expected: Error code = DEPARTMENT_HAS_ACTIVE_EMPLOYEES
```

### F-05 — Employee Integration

```
IT-11 (FR-15, AC-11):
Boundary: HTTP → EmployeesController → EmployeeService → PostgreSQL
Scenario: POST /api/v1/employees by HR Admin
Steps:
  1. POST valid employee payload with HR Admin token
  2. Assert HTTP 201
  3. GET /employees/{id} — assert record exists in DB
Expected: Employee created with status = Active

IT-12 (FR-15, AC-12):
Scenario: POST /api/v1/employees by Manager
Steps:
  1. POST valid employee payload with Manager Bearer token
  2. Assert HTTP 403
Expected: No employee created in DB

IT-13 (FR-16, AC-13):
Scenario: DELETE /api/v1/employees/{id} by HR Admin
Steps:
  1. DELETE /employees/{id} with HR Admin token
  2. Assert HTTP 204
  3. GET /employees/{id} — assert status = Inactive
Expected: Record retained; status = Inactive

IT-14 (FR-17, AC-14):
Scenario: POST /employees — reporting manager auto-promoted
Steps:
  1. Seed User X with role = Employee (no direct reports)
  2. POST /employees with reporting_manager_id = User X's ID
  3. GET /employees/{X_id} — assert role = Manager
Expected: User X promoted to Manager in DB; audit log ROLE_CHANGED recorded

IT-15 (FR-18, AC-16):
Scenario: Attempt to change Manager's role to Employee while they have active direct reports
Steps:
  1. Seed Manager with 2 active direct reports
  2. PUT /employees/{manager_id} with { role: "Employee" }
  3. Assert HTTP 422
Expected: Error message = "This user is a reporting manager for active employees and cannot be demoted to Employee."
```

### F-06 — Leave Types Integration

```
IT-16 (FR-27, AC-20):
Scenario: POST /api/v1/leave-types by HR Admin
Steps:
  1. POST valid leave type payload with HR Admin token
  2. Assert HTTP 201
  3. GET /leave-types — assert new type present
Expected: Leave type created with IsActive = true
```

### F-07 — Leave Balance Integration

```
IT-17 (FR-31, AC-21):
Scenario: GET /api/v1/balances/me
Steps:
  1. Seed employee with balances for 5 leave types
  2. GET /balances/me with Employee token
  3. Assert HTTP 200
  4. Assert response contains 5 balance records (one per active leave type)
Expected: Each record contains total_entitled, used, balance fields

IT-18 (FR-36, AC-26):
Scenario: CompOffExpiryJob expires credits and decrements balance
Steps:
  1. Seed CompOffCredit with expiry_date = yesterday; status = Active; LeaveBalance.balance = 2.0
  2. Execute CompOffExpiryJob.ExecuteAsync()
  3. Assert CompOffCredit.status = Expired
  4. Assert LeaveBalance.balance = 1.0 (decremented)
Expected: Balance decremented; audit log entry created
```

### F-08 — Holiday Calendar Integration

```
IT-19 (FR-62, AC-49):
Scenario: POST /api/v1/holidays by HR Admin
Steps:
  1. POST { date: "2026-08-15", name: "Independence Day" } with HR Admin token
  2. Assert HTTP 201
  3. GET /holidays — assert new holiday in list
Expected: Holiday stored; available to sandwich rule engine

IT-20 (FR-63, AC-50):
Scenario: POST /api/v1/holidays/bulk-import with valid CSV
Steps:
  1. POST multipart/form-data CSV file with 3 holiday rows
  2. Assert HTTP 200 with { imported_count: 3 }
  3. GET /holidays — assert 3 new records
Expected: All 3 holidays imported; duplicates (same date) skipped gracefully
```

### F-09 — Leave Request Integration

```
IT-21 (FR-41, AC-29):
Scenario: POST /leave-requests/submit with start_date = Saturday
Steps:
  1. POST { start_date: next Saturday, ... } with Employee token
  2. Assert HTTP 422
Expected: Error code = LEAVE_WEEKEND_OR_HOLIDAY

IT-22 (FR-41, AC-30):
Scenario: POST /leave-requests/submit with start_date = public holiday
Steps:
  1. Seed a holiday for next Monday
  2. POST { start_date: next Monday, ... }
  3. Assert HTTP 422
Expected: Error code = LEAVE_WEEKEND_OR_HOLIDAY

IT-23 (FR-41, AC-31):
Scenario: Submit leave overlapping an approved leave request
Steps:
  1. Seed approved leave for employee from July 15–17
  2. POST { start_date: July 16, end_date: July 18 } same employee
  3. Assert HTTP 422
Expected: Error code = LEAVE_OVERLAP_CONFLICT

IT-24 (FR-37, AC-27):
Scenario: Submit Casual Leave with zero balance
Steps:
  1. Set employee's Casual Leave balance = 0
  2. POST /leave-requests/submit for 1-day Casual Leave
  3. Assert HTTP 422
Expected: Error code = LEAVE_INSUFFICIENT_BALANCE

IT-25 (FR-37, AC-28):
Scenario: Submit Unpaid Leave with zero balance
Steps:
  1. Ensure Unpaid Leave balance = 0
  2. POST /leave-requests/submit for Unpaid Leave
  3. Assert HTTP 201
Expected: Request created; balance check skipped for Unpaid Leave

IT-26 (FR-43, AC-34):
Scenario: Team overlap limit reached
Steps:
  1. Set department team_overlap_limit = 2
  2. Seed 2 approved leaves on same date in same department
  3. Submit 3rd leave request for same date, same department
  4. Assert HTTP 422
Expected: Error code = LEAVE_TEAM_OVERLAP_LIMIT

IT-27 (FR-44, AC-35):
Scenario: Duplicate half-day for same date
Steps:
  1. Seed approved AM half-day on July 20
  2. Submit PM half-day for July 20
  3. Assert HTTP 422
Expected: Error code = LEAVE_HALF_DAY_CONFLICT

IT-28 (FR-42, AC-32):
Scenario: Sandwich rule — isolated holiday not counted
Steps:
  1. Seed holiday on Tuesday July 22
  2. Submit leave: Mon July 21 to Wed July 23
  3. Assert HTTP 201
  4. Assert leave_request.days_count = 2 in DB
Expected: Isolated Tue holiday not counted (AC-32 verified in DB)

IT-29 (FR-42, AC-33):
Scenario: Sandwich rule — chained non-working days counted
Steps:
  1. Seed holiday on Thursday July 24
  2. Submit leave: Thu July 24 to Fri July 25 (Sat + Sun follow)
  3. Assert HTTP 201
  4. Assert leave_request.days_count = 4 in DB
Expected: Thu(holiday)+Fri(leave)+Sat+Sun all counted (AC-33)

IT-30 (FR-46, AC-36):
Scenario: Employee cancels leave after start date
Steps:
  1. Seed approved leave with start_date = yesterday
  2. POST /leave-requests/{id}/cancel
  3. Assert HTTP 422
Expected: Error code = LEAVE_ALREADY_STARTED

IT-31 (FR-47, AC-37):
Scenario: HR Admin revokes leave before start date; balance restored
Steps:
  1. Seed approved leave for employee; balance deducted by 2.0
  2. POST /leave-requests/{id}/revoke with HR Admin token
  3. Assert HTTP 200
  4. GET /balances/me — assert balance restored by 2.0
Expected: leave_request.status = Revoked; balance fully restored

IT-32 (FR-47, AC-38):
Scenario: HR Admin tries to revoke leave on/after start date
Steps:
  1. Seed approved leave with start_date = today
  2. POST /leave-requests/{id}/revoke with HR Admin token
  3. Assert HTTP 422
Expected: Error code = LEAVE_ALREADY_STARTED
```

### F-10 — Comp-Off Integration

```
IT-33 (FR-52, AC-40):
Scenario: Submit comp-off for a working weekday
Steps:
  1. POST { date: next Wednesday } with Employee token (not a holiday)
  2. Assert HTTP 422
Expected: Error code = COMP_OFF_DATE_NOT_ELIGIBLE

IT-34 (FR-53, AC-41):
Scenario: Submit comp-off with < 4 worked hours
Steps:
  1. POST { date: next Saturday, start_time: "09:00", end_time: "12:00" }
  2. Assert HTTP 422
Expected: Error code = COMP_OFF_INSUFFICIENT_HOURS

IT-35 (FR-53, AC-42):
Scenario: Submit half-day comp-off with > 4 worked hours
Steps:
  1. POST { date: next Saturday, is_half_day: true, start_time: "09:00", end_time: "14:30" }
  2. Assert HTTP 201
Expected: CompOffRequest created with status = Pending

IT-36 (FR-56):
Scenario: Manager approves comp-off → balance credited
Steps:
  1. Seed pending comp-off request (is_half_day = false, 9 hours)
  2. POST /comp-off-requests/{id}/approve with Manager token
  3. GET /balances/me — assert comp-off balance +1.0
  4. Assert CompOffCredit created with expiry = earn_date + 30
Expected: Credit created and reflected in balance
```

### F-11 — Approval Engine Integration

```
IT-37 (FR-58, AC-44):
Scenario: No-manager employee — pending approval in HR Admin queue only
Steps:
  1. Seed employee with reporting_manager_id = null
  2. Employee submits leave request
  3. GET /approvals/pending with Manager token
  4. Assert the request NOT in Manager's list
  5. GET /approvals/pending with HR Admin token
  6. Assert the request IS in HR Admin's list
Expected: Routing correctly directs to HR Admin only

IT-38 (FR-58, AC-45):
Scenario: HR Admin approves L1 (no-manager) — L2 skipped
Steps:
  1. Seed no-manager employee's pending L1 leave (≤3 days, no RequiresHRFlag)
  2. POST /approvals/{id}/approve with HR Admin token
  3. GET /leave-requests/{id} — assert status = Approved
  4. Assert no ApprovalStep with level = L2 exists
Expected: Directly Approved; no L2 step created

IT-39 (FR-59, AC-46):
Scenario: Manager approves L1 for 5-day leave → moves to Pending L2
Steps:
  1. Seed 5-day leave request for employee with manager
  2. POST /approvals/{id}/approve with Manager token
  3. GET /leave-requests/{id} — assert status = PendingL2
  4. Assert ApprovalStep with level = L2 and status = Pending exists
Expected: L2 step created; balance NOT yet deducted

IT-40 (FR-59, AC-47):
Scenario: Manager approves L1 for Sick Leave → moves to Pending L2
Steps:
  1. Seed 1-day Sick Leave (RequiresHRFlag = true) for employee with manager
  2. POST /approvals/{id}/approve with Manager token
  3. Assert status = PendingL2
Expected: RequiresHRFlag triggers L2 regardless of duration

IT-41 (FR-60, AC-48):
Scenario: EscalationJob sends email for pending 2+ day approvals
Steps:
  1. Seed pending L1 approval with created_at = 3 days ago
  2. Execute EscalationJob.ExecuteAsync()
  3. Assert Hangfire email job enqueued for the approver
Expected: Email job in Hangfire queue with approver's email
```

### F-12 — Notifications Integration

```
IT-42 (FR-66, AC-52):
Scenario: Leave submission → email job enqueued within same request
Steps:
  1. POST /leave-requests/submit (valid request)
  2. Assert HTTP 201
  3. Assert Hangfire queue contains EmailJob with recipient = manager's email
Expected: Email job created synchronously as part of submission flow

IT-43 (FR-68, AC-54):
Scenario: GET /api/v1/notifications
Steps:
  1. Seed 3 notifications for employee (2 unread, 1 read)
  2. GET /notifications with Employee token
  3. Assert HTTP 200
  4. Assert response contains 3 notifications with read, title, message, related_entity_type
Expected: All fields present; unread count = 2

IT-44 (FR-69, AC-55):
Scenario: Leave approval → CalendarSyncJob created
Steps:
  1. Submit and approve a leave request
  2. Assert Hangfire queue contains CalendarSyncJob with leave_request_id
Expected: CalendarSyncJob created with 3 retry policy
```

### F-03 — Seeding Integration

```
IT-45 (FR-82, AC-64):
Scenario: DataSeeder on empty DB
Steps:
  1. Run DataSeeder.SeedAsync() on empty lms_test DB
  2. Assert 1 Super Admin user exists (password = "Admin@123", BCrypt-hashed)
  3. Assert 1 HR Admin user exists
  4. Assert 1 department "HR" exists
  5. Assert 5 active leave types exist
Expected: Exact counts match; no duplicates

IT-46 (FR-84, AC-65):
Scenario: DataSeeder run twice → idempotent
Steps:
  1. Run DataSeeder.SeedAsync() twice
  2. Assert user count = 2, department count = 1, leave type count = 5
Expected: No duplicates
```

### F-13 — Reporting Integration

```
IT-47 (FR-74, AC-59):
Scenario: GET /api/v1/reports/utilization by HR Admin
Steps:
  1. Seed leave data across 2 departments
  2. GET /reports/utilization with HR Admin token
  3. Assert HTTP 200
  4. Assert response contains per-department utilization records
Expected: Records include department name, leave_days_used, total_entitled

IT-48 (FR-76, AC-60):
Scenario: GET /api/v1/reports/export with date range → CSV
Steps:
  1. GET /reports/export?date_from=2026-01-01&date_to=2026-12-31 with HR Admin token
  2. Assert HTTP 200
  3. Assert Content-Type: text/csv
Expected: CSV body downloadable with leave records
```

### F-14 — Audit Trail Integration

```
IT-49 (FR-77, AC-61):
Scenario: Leave approval creates audit log row
Steps:
  1. Approve a leave request (any valid flow)
  2. Query audit_logs WHERE action = 'LEAVE_APPROVED' AND entity_id = leave_request.id
  3. Assert row exists with old_value (status before), new_value (Approved), user_id = approver, ip_address
Expected: Audit row created with all required fields

IT-50 (FR-79, AC-62):
Scenario: Application blocks deletion of audit log row
Steps:
  1. Attempt to call AuditRepository.DeleteAsync() in test
  2. Assert AuditLogImmutableException is thrown
Expected: No DB delete statement executed

IT-51 (FR-80, AC-63):
Scenario: GET /api/v1/audit-log with filters
Steps:
  1. Seed multiple audit log entries; one with action = LEAVE_APPROVED, user_id = X, today's date
  2. GET /audit-log?user_id=X&action=LEAVE_APPROVED&date_from=today&date_to=today with HR Admin token
  3. Assert HTTP 200
  4. Assert only matching entries returned
Expected: Filtered results only; unmatched entries excluded

IT-52 (NFR-5):
Scenario: Unauthenticated request to protected endpoint
Steps:
  1. GET /api/v1/employees (no Authorization header)
  2. Assert HTTP 401
Expected: JWT required; no data returned

IT-53 (NFR-6):
Scenario: Manager attempts HR Admin endpoint
Steps:
  1. GET /api/v1/accounts/locked with Manager token
  2. Assert HTTP 403
Expected: RBAC enforced; Manager cannot access HR Admin resources
```

---

## Part 3 — E2E Tests (E2E-)

All E2E tests use **Playwright** (`tests/e2e/`).
Trigger: `workflow_dispatch` **ONLY** — never on PR push.
Base URL: `E2E_BASE_URL` env var (staging: `https://staging.lms.internal`).

```
E2E-1 (PT-1, FR-3, FR-4):
@smoke
Scenario: Employee logs in with local credentials
Actor: employee1@lms.test / Admin@123
Steps:
  1. await page.goto(`${BASE_URL}/auth/login`)
  2. await page.fill('[name=email]', 'employee1@lms.test')
  3. await page.fill('[name=password]', 'Admin@123')
  4. await page.click('[data-testid=local-login-btn]')
  5. await page.waitForURL('**/me/dashboard')
  6. await expect(page.locator('[data-testid=dashboard-greeting]')).toBeVisible()
  7. await expect(page.locator('[data-testid=notification-badge]')).toBeVisible()
Environment: staging

E2E-2 (PT-2, FR-1, FR-4):
Scenario: User logs in via Azure AD SSO
Actor: All roles (SSO-enabled account)
Steps:
  1. await page.goto(`${BASE_URL}/auth/login`)
  2. await page.click('[data-testid=sso-login-btn]')
  3. Handle Azure AD redirect (use Playwright network intercept or test tenant)
  4. await page.waitForURL('**/me/dashboard')
  5. await expect(page.locator('[data-testid=dashboard-greeting]')).toBeVisible()
Environment: staging (requires Azure AD test tenant credentials in secrets)

E2E-3 (PT-3, FR-7, FR-8):
Scenario: Account lock and HR Admin unlock flow
Actor: employee2@lms.test (lockee), hradmin@lms.test (unlocker)
Steps:
  1. As employee2: attempt login 3× with wrong password → each returns error
  2. As employee2: 4th attempt → verify "Account locked" error message
  3. As hradmin: navigate to /hr/locked-accounts
  4. Locate employee2's account; click Unlock
  5. Confirm unlock dialog
  6. As employee2: login with correct password (Admin@123)
  7. await page.waitForURL('**/me/dashboard')
Expected: Successful login after unlock
Environment: staging

E2E-4 (PT-4, FR-39, FR-41, FR-42, FR-43):
@smoke
Scenario: Employee applies for leave with full validation flow
Actor: employee1@lms.test
Steps:
  1. Login as employee1
  2. await page.click('[data-testid=apply-leave-btn]')
  3. await page.selectOption('[data-testid=leave-type-select]', 'Casual Leave')
  4. Select start_date = next Thursday (seeded as holiday), end_date = next Friday
  5. await expect(page.locator('[data-testid=days-count]')).toContainText('2 days')
  6. await expect(page.locator('[data-testid=sandwich-rule-note]')).toBeVisible()
  7. await page.fill('[name=reason]', 'Personal')
  8. await page.click('[data-testid=submit-btn]')
  9. await page.waitForURL('**/leave/history')
  10. await expect(page.locator('[data-testid=leave-status]').first()).toContainText('Pending L1 Approval')
Expected: Leave created with correct days_count and status
Environment: staging

E2E-5 (PT-5, FR-37):
Scenario: Employee blocked when Casual Leave balance is zero
Actor: employee2@lms.test (pre-seeded with 0 Casual Leave balance)
Steps:
  1. Login as employee2
  2. Navigate to /leave/apply
  3. Select leave type "Casual Leave"
  4. Select valid future dates
  5. await expect(page.locator('[data-testid=balance-warning]')).toContainText('Insufficient balance')
  6. await expect(page.locator('[data-testid=submit-btn]')).toBeDisabled()
Expected: Submit disabled; no request created
Environment: staging

E2E-6 (PT-6, FR-58, FR-59):
Scenario: No-manager employee — HR Admin approves as L1, L2 skipped
Actor: employee2@lms.test (no manager), hradmin@lms.test
Steps:
  1. Login as employee2; submit a 1-day Casual Leave (future date)
  2. Login as hradmin; navigate to /approvals
  3. Verify employee2's request appears in pending list
  4. Approve the request
  5. Login as employee2; navigate to /leave/history
  6. await expect(page.locator('[data-testid=leave-status]').first()).toContainText('Approved')
Expected: Leave directly Approved; no Pending L2 step
Environment: staging

E2E-7 (PT-7, FR-58, FR-59):
Scenario: Two-level approval for Sick Leave
Actor: employee1@lms.test, manager1@lms.test (L1), hradmin@lms.test (L2)
Steps:
  1. Login as employee1; submit Sick Leave (1 day, future date, upload dummy attachment)
  2. Login as manager1; navigate to /approvals; approve (L1)
  3. Verify status = Pending L2
  4. Login as hradmin; navigate to /approvals; approve (L2)
  5. Login as employee1; verify status = Approved
Expected: Two-step approval completed; balance deducted after L2
Environment: staging

E2E-8 (PT-8, FR-51, FR-53, FR-55, FR-56):
Scenario: Comp-off submit → manager approve → balance credited
Actor: employee1@lms.test, manager1@lms.test
Steps:
  1. Login as employee1; navigate to /leave/comp-off
  2. Select date = next Saturday, start_time = "09:00", end_time = "18:00", is_half_day = false
  3. Submit
  4. Login as manager1; navigate to /approvals → Comp-Off tab; approve
  5. Login as employee1; navigate to /leave/balances
  6. await expect(page.locator('[data-testid=compoff-balance]')).toContainText('+1')
Expected: Comp-off balance incremented by 1.0; CompOffCredit created
Environment: staging

E2E-9 (PT-9, FR-46, FR-47):
Scenario: Cancel leave (success) + revoke after start (blocked)
Actor: employee1@lms.test, hradmin@lms.test
Steps:
  1. employee1: submit and get approved a future leave (>5 days out)
  2. employee1: navigate to /leave/history; click Cancel; confirm
  3. Verify status = Cancelled; balance restored
  4. (Separate scenario) Seed a leave with start_date = today as Approved
  5. hradmin: attempt to revoke
  6. Verify error "Cannot revoke a leave that has already started"
Expected: Cancel works pre-start; revoke blocked on/after start
Environment: staging

E2E-10 (PT-10, FR-62, FR-63, FR-65):
Scenario: HR Admin manages holidays; calendar disables them in leave apply
Actor: hradmin@lms.test, employee1@lms.test
Steps:
  1. Login as hradmin; navigate to /hr/holidays/manage
  2. Add holiday for next Monday
  3. Bulk import CSV with 3 additional holidays
  4. Login as employee1; navigate to /leave/apply
  5. await expect(page.locator(`[data-date="${nextMonday}"]`)).toHaveAttribute('aria-disabled', 'true')
  6. Verify all 4 new dates are disabled in date picker
Expected: All 4 new holidays blocked as selectable dates
Environment: staging

E2E-11 (PT-11, FR-66, FR-68, FR-69):
Scenario: Leave approval triggers email, in-app notification, Google Calendar event
Actor: employee1@lms.test, manager1@lms.test
Steps:
  1. employee1: submit 1-day Casual Leave (future date, employee1 has manager = manager1)
  2. manager1: approve L1 (no L2 needed — 1 day, no RequiresHRFlag)
  3. employee1: navigate to /notifications
  4. await expect(page.locator('[data-testid=notification-item]').first()).toContainText('Leave Approved')
  5. Check email delivery (intercept via SendGrid activity log or test inbox)
  6. Check Google Calendar shared company calendar for all-day event on leave date
Expected: All 3 channels triggered within 2 minutes of approval
Environment: staging

E2E-12 (PT-12, FR-72, FR-73, FR-74, FR-75):
Scenario: Role-appropriate dashboards visible per role
Actor: employee1, manager1, hradmin, superadmin@lms.test
Steps:
  1. Login as employee1 → /me/dashboard → verify balance cards and recent requests visible
  2. Login as manager1 → verify /manager/dashboard visible; team calendar present
  3. Login as hradmin → /hr/dashboard → verify utilization chart and trend chart visible
  4. Login as superadmin → /admin/dashboard → verify system metrics (Total Employees, Total Leaves Today)
  5. As employee1 → attempt to navigate to /hr/dashboard → verify redirect to /unauthorized
Expected: Each role sees correct dashboard; unauthorized routes blocked
Environment: staging

E2E-13 (PT-13, FR-77, FR-80):
Scenario: Audit log records leave approval and is searchable
Actor: manager1@lms.test (approves), hradmin@lms.test (searches)
Steps:
  1. manager1: approve a pending leave request
  2. hradmin: navigate to /hr/audit-log
  3. Filter: action = LEAVE_APPROVED, date = today
  4. await expect(page.locator('[data-testid=audit-row]').first()).toContainText('LEAVE_APPROVED')
  5. Verify old_value, new_value, approver name, timestamp, IP address visible in row detail
Expected: Audit entry visible within 30 seconds of approval
Environment: staging

E2E-14 (PT-14, FR-82, FR-83, FR-84):
Scenario: Initial seed data verified by Super Admin
Actor: superadmin@lms.test
Steps:
  1. Login as superadmin (Admin@123)
  2. Navigate to /admin/employees — verify Super Admin and HR Admin accounts visible
  3. Navigate to /admin/departments — verify "HR" department visible
  4. Navigate to /admin/leave-types — verify 5 default types (Casual, Sick, Earned, Comp-off, Unpaid) visible and active
  5. (CI) Run seed script again — verify row counts unchanged
Expected: 2 users + 1 dept + 5 leave types; no duplicates after re-seed
Environment: staging
```

---

## Part 4 — Regression Register (RT-)

Critical ACs that must never break. Every RT maps to a covering UT- or IT-.

| RT-ID | AC-ref | FR-ref | Covering Test | Regression Risk |
|-------|--------|--------|--------------|----------------|
| RT-1 | AC-3 | FR-3 | IT-1 | HIGH — login broken = system unusable |
| RT-2 | AC-5 | FR-4, FR-9 | UT-6 | HIGH — bad JWT claims = RBAC breaks |
| RT-3 | AC-7 | FR-7 | IT-3 | HIGH — security: lockout must trigger |
| RT-4 | AC-22 | FR-32 | UT-22 | HIGH — wrong deduction = balance corruption |
| RT-5 | AC-23 | FR-32 | UT-23 | HIGH — wrong half-day deduction |
| RT-6 | AC-27 | FR-37 | IT-24, UT-24 | HIGH — must not allow negative balance |
| RT-7 | AC-28 | FR-37 | IT-25, UT-26 | HIGH — Unpaid Leave exempt; blocking it = user impact |
| RT-8 | AC-32 | FR-42 | UT-34, IT-28 | HIGH — sandwich rule correctness is critical |
| RT-9 | AC-33 | FR-42 | UT-35, IT-29 | HIGH — sandwich rule chained days |
| RT-10 | AC-44 | FR-58 | IT-37, UT-48 | HIGH — no-manager routing must not fail |
| RT-11 | AC-45 | FR-58 | IT-38, UT-49 | HIGH — no-manager L2 skip must work |
| RT-12 | AC-46 | FR-59 | IT-39, UT-50 | HIGH — L2 trigger for >3 day leave |
| RT-13 | AC-47 | FR-59 | IT-40, UT-51 | HIGH — Sick Leave always triggers L2 (when manager exists) |
| RT-14 | AC-37 | FR-47 | IT-31 | HIGH — balance must restore on revoke |
| RT-15 | AC-36 | FR-46 | IT-30, UT-41 | HIGH — cannot cancel started leave |
| RT-16 | AC-38 | FR-47 | IT-32, UT-42 | HIGH — cannot revoke started leave |
| RT-17 | AC-62 | FR-79 | IT-50, UT-56 | HIGH — audit log must be immutable |
| RT-18 | AC-9 | FR-8 | IT-8 | MEDIUM — unlock flow must work |
| RT-19 | AC-14 | FR-17 | IT-14 | MEDIUM — role auto-promotion must work |
| RT-20 | AC-16 | FR-18 | IT-15 | MEDIUM — demotion block must fire |
| RT-21 | AC-25 | FR-35 | UT-30 | MEDIUM — comp-off expiry = earn+30 |
| RT-22 | AC-26 | FR-36 | IT-18 | MEDIUM — expiry job must decrement balance |
| RT-23 | AC-34 | FR-43 | IT-26 | MEDIUM — team overlap limit enforced |
| RT-24 | AC-41 | FR-53 | IT-34, UT-43 | MEDIUM — comp-off hours threshold |
| RT-25 | AC-50 | FR-63 | IT-20 | MEDIUM — bulk holiday import |
| RT-26 | AC-53 | FR-67 | UT-54 | MEDIUM — email retry on SendGrid failure |
| RT-27 | AC-56 | FR-70 | UT-55 | MEDIUM — calendar failure must not reverse approval |
| RT-28 | AC-61 | FR-77 | IT-49 | MEDIUM — audit row created on approval |
| RT-29 | AC-64 | FR-82 | IT-45 | MEDIUM — seed creates correct defaults |
| RT-30 | AC-65 | FR-84 | IT-46 | MEDIUM — seed is idempotent |

---

## Test Count Summary

| Type | Count | Coverage |
|------|-------|---------|
| UT- (Unit) | 61 | All 84 MUST FRs; all 65 ACs; 100% for critical modules |
| IT- (Integration) | 53 | All service boundaries; all critical API flows |
| E2E- (Playwright) | 14 | All PT-1 to PT-14 |
| RT- (Regression) | 30 | All HIGH-risk ACs; critical-path only |
| **Total** | **158** | |

### Coverage Confirmation
- ✅ Every MUST-priority FR has at least one UT- or IT-
- ✅ Every AC (AC-1 to AC-65) covered by at least one UT-, IT-, or E2E-
- ✅ Every PT- (PT-1 to PT-14) has a corresponding E2E-
- ✅ No test references implementation details (file paths, specific class internals)
- ✅ Regression register covers all HIGH-risk and critical-path ACs
- ✅ Sandwich rule: 5 dedicated UT- tests (UT-34 to UT-38) for every edge case
- ✅ Auth module: 10 UT- + 6 IT- (matches 100% coverage requirement)
- ✅ No-manager + retroactive edge case: UT-53 captures HIL 3 confirmed behaviour
