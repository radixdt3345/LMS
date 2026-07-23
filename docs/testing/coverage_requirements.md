# Coverage Requirements — Leave Management System (LMS)

Derived from CONSTITUTION.md Article III + PRD critical-path FRs.

## Gates

| Gate | Threshold | Enforcement |
|------|-----------|------------|
| Project-wide minimum | **80%** line coverage | CI blocks PR merge below this |
| Project-wide target | **90%** line coverage | Tracked per PR; aspirational |
| Critical paths | **100%** line coverage | CI enforces per-module thresholds |

---

## Per-Module Coverage Requirements

| Module / Service | Coverage Gate | Coverage Target | Notes |
|-----------------|--------------|----------------|-------|
| **SandwichRuleEngine** | **100%** | 100% | FR-42 — complex algorithm; every edge case must be tested |
| **LeaveBalanceService** (deduct, restore, prorate, lapse) | **100%** | 100% | FR-32, FR-33, FR-37, FR-38 — financial correctness |
| **AuthService** + **TokenService** | **100%** | 100% | FR-1 to FR-11 — auth is a MUST security boundary |
| **ApprovalService** (L1/L2 routing, no-manager, skip-L2) | **100%** | 100% | FR-58, FR-59 — approval engine logic is safety-critical |
| **AccountService** (lock/unlock) | **100%** | 100% | FR-7, FR-8 — security |
| **RBAC enforcement layer** (AuthorizationPolicies, RoleProtectedRoute) | **100%** | 100% | CONSTITUTION Article IV — every access control path |
| LeaveRequestService | 90% | 95% | FR-39–FR-50 — core feature |
| CompOffRequestService | 90% | 95% | FR-51–FR-57 |
| CompOffCreditService + expiry job | 90% | 95% | FR-35, FR-36 |
| HolidayService | 85% | 90% | FR-62–FR-65 |
| EmployeeService (role derivation) | 90% | 95% | FR-17, FR-18 — role auto-derivation |
| DepartmentService | 85% | 90% | FR-21–FR-26 |
| LeaveTypeService | 85% | 90% | FR-27–FR-30 |
| EscalationJob | 90% | 90% | FR-60 — background job |
| YearEndLapseJob + NewYearCreditJob | 90% | 90% | FR-38 — financial correctness |
| NotificationService | 85% | 90% | FR-66–FR-71 |
| EmailService (SendGrid) | 80% | 85% | FR-67 — failure handling paths |
| CalendarService (Google) | 80% | 85% | FR-69, FR-70 |
| AuditService | 90% | 95% | FR-77–FR-81 — compliance |
| ReportService | 85% | 90% | FR-72–FR-76 |
| Controllers | 80% | 85% | Thin layer; tested via L4 API tests |
| **Frontend — Redux slices** | 85% | 90% | State management correctness |
| **Frontend — LeaveBalanceCard** | 85% | 90% | FR-72 |
| **Frontend — ApplyLeaveForm** | 90% | 95% | FR-39, FR-41, FR-65 — client-side validation display |
| **Frontend — SandwichRuleDisplay** | 90% | 95% | FR-42 display logic |
| **Frontend — ProtectedRoute + RoleProtectedRoute** | 100% | 100% | Security gate; every role combination tested |

---

## Critical Path Test Coverage Detail

### Sandwich Rule (100% required)
All of the following scenarios must have dedicated unit tests:

| Scenario | FR Reference |
|---------|-------------|
| Isolated holiday between two leave days → NOT counted | FR-42 |
| Holiday on left of leave day (chained) → counted | FR-42 |
| Holiday on right of leave day (chained) → counted | FR-42 |
| Weekend block chained to leave days on both sides → counted | FR-42 |
| Non-working days at edges of range → counted | FR-42 |
| Non-working day bridging two separate requests → NOT counted | FR-42 |
| Thu holiday + Fri leave + Sat + Sun = 4 days | FR-42 |
| Mon holiday + Tue leave + Wed holiday = 3 days | FR-42 |
| Mon leave + Tue holiday + Wed leave = 2 days (isolated) | FR-42 |

### Leave Balance (100% required)
| Scenario | FR Reference |
|---------|-------------|
| Full-day deducts exactly 1.0 | FR-32 |
| Half-day deducts exactly 0.5 | FR-32 |
| Balance restored on cancel (full-day and half-day) | FR-46 + FR-10 |
| Balance restored on revoke | FR-47 |
| Zero balance → InsufficientBalanceException | FR-37 |
| Would-go-negative → InsufficientBalanceException | FR-37 |
| Unpaid Leave → no balance check (always allowed) | FR-37 |
| Mid-year joiner July 1 (184 remaining days) → 6.0 days for Casual | FR-33 |
| Mid-year joiner Dec 30 (1 remaining day) → 0.5 days minimum | FR-33 |
| Mid-year joiner Dec 31 (0 remaining days) → 0.0 days | FR-33 |
| Year-end lapse zeros all balances | FR-38 |

### Auth (100% required)
| Scenario | FR Reference |
|---------|-------------|
| Valid JWT issued with correct claims | FR-4, FR-9 |
| Refresh token invalidated on logout | FR-5 |
| 1st, 2nd failed login → not locked | FR-7 |
| 3rd failed login → account locked | FR-7 |
| 4th attempt on locked account → 423 | FR-7 |
| Unlock by HR Admin → login succeeds | FR-8 |
| Password < 8 chars → rejected | FR-6 |
| Password no uppercase → rejected | FR-6 |
| Password no number → rejected | FR-6 |
| Unknown SSO user → Employee role assigned | FR-11 |

---

## coverlet Configuration

```xml
<!-- LMS.Tests/LMS.Tests.csproj -->
<PackageReference Include="coverlet.collector" Version="6.*" />
```

```bash
# CI run with threshold enforcement
dotnet test LMS.Tests \
  --collect:"XPlat Code Coverage" \
  /p:Threshold=80 \
  /p:ThresholdType=line \
  /p:ThresholdStat=average
```

For per-module 100% enforcement, use coverlet's `--threshold-stat total` on filtered test runs targeting the critical modules.
