# Test Environments — Leave Management System (LMS)

> Note: No Docker in Phase 1 (HIL 2 confirmed). Test environments use native PostgreSQL.

## Environment Summary

| Environment | Purpose | PostgreSQL | Setup | Data Strategy | Teardown |
|-------------|---------|-----------|-------|--------------|----------|
| Local (dev) | Unit + integration tests during development | Native local install (`lms_test` DB) | `dotnet ef database update --connection "..."` | Fresh seed per test run; in-memory for unit tests | `DROP DATABASE lms_test` (test fixture handles) |
| CI (GitHub Actions) | Automated L1–L4 on every PR | PostgreSQL service in GitHub Actions runner | `dotnet ef database update` in CI step | Clean DB per CI run; idempotent seed | Runner teardown (ephemeral) |
| Staging | L5 perf, L6 security, L7 smoke, L8 UAT | Native install on staging server (`lms` DB) | Deployed via deploy script | Persistent test accounts; prod-like volume | Manual cleanup of test data by QA |
| Production | L10 rollback validation only | Native install on prod server | N/A | Real data; read-only spot checks | N/A |

---

## Local Development

### Setup
```bash
# 1. Ensure PostgreSQL 15+ is running locally
# 2. Create test database
psql -U postgres -c "CREATE DATABASE lms_test;"

# 3. Apply migrations to test DB
dotnet ef database update \
  --project LMS.Infrastructure \
  --startup-project LMS.API \
  --connection "Host=localhost;Database=lms_test;Username=postgres;Password=..."

# 4. Run all tests
dotnet test LMS.Tests

# 5. Run frontend tests
cd frontend && npm run test
```

### Test DB Connection
- Set via environment variable or `appsettings.Test.json`:
  ```
  ConnectionStrings__TestConnection=Host=localhost;Database=lms_test;Username=postgres;Password=...
  ```
- `LmsTestFixture.cs` picks up `ConnectionStrings__TestConnection`; falls back to `DefaultConnection` + `_test` suffix

### Data Strategy (Local Integration Tests)
- Each `IClassFixture<LmsTestFixture>` creates the schema fresh and seeds minimal data
- Each test class that writes data uses `IAsyncLifetime.DisposeAsync` to clean up its rows
- Unit tests: fully in-memory (no DB); all repositories mocked via Moq

---

## CI Environment (GitHub Actions)

### PostgreSQL Service
```yaml
# .github/workflows/ci.yml
services:
  postgres:
    image: postgres:15
    env:
      POSTGRES_DB: lms_test
      POSTGRES_USER: ci_user
      POSTGRES_PASSWORD: ${{ secrets.CI_DB_PASSWORD }}
    ports:
      - 5432:5432
    options: >-
      --health-cmd pg_isready
      --health-interval 10s
      --health-timeout 5s
      --health-retries 5
```

### CI Steps for Test DB
```bash
# In CI workflow after postgres service is healthy:
dotnet ef database update \
  --connection "Host=localhost;Port=5432;Database=lms_test;Username=ci_user;Password=..."
dotnet test LMS.Tests \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults
```

### Data Strategy (CI)
- CI runner is ephemeral; DB is fresh on every run
- No test data leakage between runs
- Seed runs via `LmsTestFixture` in each integration test class

### Secrets (CI)
- `CI_DB_PASSWORD` — GitHub Encrypted Secret
- `JWT_TEST_SECRET` — JWT signing key for test tokens (separate from prod key)
- No real SendGrid/Google/Azure credentials in CI — all mocked

---

## Staging Environment

### Purpose
- L5 Performance tests (k6 against staging API)
- L6 Security scans (OWASP ZAP against staging)
- L7 Smoke tests (Playwright `@smoke` suite)
- L8 UAT (manual + guided Playwright scripts)

### Test Accounts (pre-seeded, persistent)
| Role | Email | Password |
|------|-------|---------|
| Super Admin | superadmin@lms.test | Admin@123 |
| HR Admin | hradmin@lms.test | Admin@123 |
| Manager (has subordinates) | manager1@lms.test | Admin@123 |
| Employee (with manager) | employee1@lms.test | Admin@123 |
| Employee (no manager) | employee2@lms.test | Admin@123 |

### Data Strategy (Staging)
- Persistent staging DB with test accounts always present
- QA team responsible for cleaning up test leave requests after UAT
- Performance test runs may pollute DB with test leave requests — clean up via HR Admin after k6 run
- E2E Playwright smoke tests target staging; use a dedicated test employee account to avoid polluting real data

### Staging URL
- Backend API: `https://staging.lms.internal/api/v1/`
- Frontend SPA: `https://staging.lms.internal/`
- Set as `E2E_BASE_URL` in Playwright config for staging smoke tests

---

## Test Data Builders

`LMS.Tests/Helpers/TestDataBuilder.cs` provides fluent builders for all test entities:

```csharp
// Usage in tests:
var employee = TestDataBuilder.Employee()
    .WithDepartment(departmentId)
    .WithReportingManager(managerId)
    .WithRole("Employee")
    .Build();

var leaveRequest = TestDataBuilder.LeaveRequest()
    .ForEmployee(employeeId)
    .WithLeaveType(casualLeaveTypeId)
    .From(DateTime.Today.AddDays(5))
    .To(DateTime.Today.AddDays(7))
    .Build();
```

`JwtTestHelper.cs` generates test JWTs with configurable claims for RBAC testing:

```csharp
var managerToken = JwtTestHelper.GenerateToken(
    userId: managerId,
    role: "Manager",
    departmentId: deptId
);
```
