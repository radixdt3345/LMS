using LMS.Application.DTOs.LeaveRequest;
using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Domain.Services;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Implements leave request lifecycle: Draft → Pending → Approved/Rejected/Cancelled/Revoked.
/// Applies the SandwichRuleEngine for leave-day computation and the ApprovalService for
/// routing. All mutations call AuditService.LogAsync (CLAUDE.md — Audit all mutations).
/// FR-38 to FR-46.
/// </summary>
public class LeaveRequestService : ILeaveRequestService
{
    private readonly LmsDbContext        _db;
    private readonly IAuditService       _audit;
    private readonly ILeaveBalanceService _balance;
    private readonly IApprovalService    _approval;

    public LeaveRequestService(
        LmsDbContext         db,
        IAuditService        audit,
        ILeaveBalanceService balance,
        IApprovalService     approval)
    {
        _db       = db;
        _audit    = audit;
        _balance  = balance;
        _approval = approval;
    }

    // ── Create (→ Draft) ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<LeaveRequestDto>> CreateRequestAsync(
        Guid employeeId, CreateLeaveRequestDto dto, CancellationToken ct = default)
    {
        if (dto.EndDate < dto.StartDate)
            return Result<LeaveRequestDto>.Failure(
                "End date must be on or after start date.");

        var leaveType = await _db.LeaveTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(lt => lt.Id == dto.LeaveTypeId && lt.IsActive, ct);

        if (leaveType is null)
            return Result<LeaveRequestDto>.Failure(
                "Leave type not found or is inactive.", 404);

        var now = DateTime.UtcNow;
        var request = new LeaveRequest
        {
            Id          = Guid.NewGuid(),
            EmployeeId  = employeeId,
            LeaveTypeId = dto.LeaveTypeId,
            StartDate   = dto.StartDate,
            EndDate     = dto.EndDate,
            Reason      = dto.Reason,
            DocumentUrl = dto.DocumentUrl,
            Status      = LeaveRequestStatus.Draft,
            CreatedAt   = now,
            UpdatedAt   = now,
        };

        _db.LeaveRequests.Add(request);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            action:     "LeaveRequest.Create",
            entityType: "LeaveRequest",
            entityId:   request.Id,
            actorId:    employeeId,
            oldValue:   null,
            newValue:   new { request.Id, Status = request.Status.ToString() });

        return Result<LeaveRequestDto>.Success(ToDto(request, leaveType));
    }

    // ── Submit (Draft → Pending) ──────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<LeaveRequestDto>> SubmitRequestAsync(
        Guid requestId, Guid callerId, CancellationToken ct = default)
    {
        var request = await _db.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.Employee)
            .Include(r => r.ApprovalSteps)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request is null)
            return Result<LeaveRequestDto>.Failure("Leave request not found.", 404);

        if (request.EmployeeId != callerId)
            return Result<LeaveRequestDto>.Failure(
                "You are not authorised to submit this request.", 403);

        if (request.Status != LeaveRequestStatus.Draft)
            return Result<LeaveRequestDto>.Failure(
                "Only Draft requests can be submitted.");

        // 1. Compute leave days via SandwichRuleEngine (UT-34 to UT-42).
        var holidays = await GetHolidaySetAsync(request.StartDate, request.EndDate, ct);
        request.ComputedDays = SandwichRuleEngine.ComputeLeaveDays(
            request.StartDate, request.EndDate, holidays);

        // 2. Retroactive flag — start date is in the past (UTC).
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        request.IsRetroactive = request.StartDate < today;

        // 3. Overlap check: reject if any Pending/Approved request for the same employee
        //    overlaps the requested range.
        var hasOverlap = await _db.LeaveRequests
            .AnyAsync(r =>
                r.Id         != requestId &&
                r.EmployeeId == request.EmployeeId &&
                (r.Status == LeaveRequestStatus.Pending ||
                 r.Status == LeaveRequestStatus.Approved) &&
                r.StartDate  <= request.EndDate &&
                r.EndDate    >= request.StartDate, ct);

        if (hasOverlap)
            return Result<LeaveRequestDto>.Failure(
                "A pending or approved leave request already overlaps with the requested dates.");

        // 4. Supporting document required?
        if (request.LeaveType.RequiresDocument && request.DocumentUrl is null)
            return Result<LeaveRequestDto>.Failure(
                "A supporting document URL is required for this leave type.");

        // 5. Balance deduction — skipped for Unlimited accrual types (e.g. Unpaid Leave, UT-26).
        if (request.LeaveType.AccrualType != AccrualType.Unlimited)
        {
            var deduct = await _balance.DeductBalance(
                request.EmployeeId, request.LeaveTypeId, request.ComputedDays);

            if (!deduct.IsSuccess)
                return Result<LeaveRequestDto>.Failure(deduct.Error!, deduct.StatusCode);
        }

        // 6. Create approval steps (no-manager rule: UT-53).
        var stepsResult = await _approval.CreateApprovalStepsAsync(
            request.Id, request.Employee, ct);

        if (!stepsResult.IsSuccess)
            return Result<LeaveRequestDto>.Failure(
                stepsResult.Error!, stepsResult.StatusCode);

        var oldStatus = request.Status;
        request.Status    = LeaveRequestStatus.Pending;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            action:     "LeaveRequest.Submit",
            entityType: "LeaveRequest",
            entityId:   request.Id,
            actorId:    callerId,
            oldValue:   new { Status = oldStatus.ToString() },
            newValue:   new { Status = request.Status.ToString(), request.ComputedDays });

        // Reload steps that were just persisted by ApprovalService.
        await _db.Entry(request).Collection(r => r.ApprovalSteps).LoadAsync(ct);

        return Result<LeaveRequestDto>.Success(ToDto(request, request.LeaveType));
    }

    // ── Cancel (Draft|Pending → Cancelled) ───────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<LeaveRequestDto>> CancelRequestAsync(
        Guid requestId, Guid callerId, CancellationToken ct = default)
    {
        var request = await _db.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.ApprovalSteps)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request is null)
            return Result<LeaveRequestDto>.Failure("Leave request not found.", 404);

        if (request.EmployeeId != callerId)
            return Result<LeaveRequestDto>.Failure(
                "You are not authorised to cancel this request.", 403);

        if (request.Status is not (LeaveRequestStatus.Draft or LeaveRequestStatus.Pending))
            return Result<LeaveRequestDto>.Failure(
                "Only Draft or Pending requests can be cancelled.");

        // Restore balance only if the request is Pending (balance was deducted on submit).
        if (request.Status == LeaveRequestStatus.Pending
            && request.LeaveType.AccrualType != AccrualType.Unlimited
            && request.ComputedDays > 0m)
        {
            await _balance.RestoreBalance(
                request.EmployeeId, request.LeaveTypeId, request.ComputedDays);
        }

        var oldStatus = request.Status;
        request.Status    = LeaveRequestStatus.Cancelled;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            action:     "LeaveRequest.Cancel",
            entityType: "LeaveRequest",
            entityId:   request.Id,
            actorId:    callerId,
            oldValue:   new { Status = oldStatus.ToString() },
            newValue:   new { Status = request.Status.ToString() });

        return Result<LeaveRequestDto>.Success(ToDto(request, request.LeaveType));
    }

    // ── Revoke (Pending|Approved → Revoked, HRAdmin+) ─────────────────────────

    /// <inheritdoc/>
    public async Task<Result<LeaveRequestDto>> RevokeRequestAsync(
        Guid requestId, Guid callerId, CancellationToken ct = default)
    {
        var request = await _db.LeaveRequests
            .Include(r => r.LeaveType)
            .Include(r => r.ApprovalSteps)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request is null)
            return Result<LeaveRequestDto>.Failure("Leave request not found.", 404);

        if (request.Status is not (LeaveRequestStatus.Pending or LeaveRequestStatus.Approved))
            return Result<LeaveRequestDto>.Failure(
                "Only Pending or Approved requests can be revoked.");

        // Restore balance for non-Unlimited leave types.
        if (request.LeaveType.AccrualType != AccrualType.Unlimited
            && request.ComputedDays > 0m)
        {
            await _balance.RestoreBalance(
                request.EmployeeId, request.LeaveTypeId, request.ComputedDays);
        }

        var oldStatus = request.Status;
        request.Status    = LeaveRequestStatus.Revoked;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            action:     "LeaveRequest.Revoke",
            entityType: "LeaveRequest",
            entityId:   request.Id,
            actorId:    callerId,
            oldValue:   new { Status = oldStatus.ToString() },
            newValue:   new { Status = request.Status.ToString(), RevokedBy = callerId });

        return Result<LeaveRequestDto>.Success(ToDto(request, request.LeaveType));
    }

    // ── Get own requests (paginated) ─────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Result<PagedResult<LeaveRequestDto>>> GetMyRequestsAsync(
        Guid employeeId, int page, int limit, CancellationToken ct = default)
    {
        page  = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 100);

        var query = _db.LeaveRequests
            .AsNoTracking()
            .Include(r => r.LeaveType)
            .Include(r => r.ApprovalSteps)
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(ct);

        return Result<PagedResult<LeaveRequestDto>>.Success(new PagedResult<LeaveRequestDto>
        {
            Items = items.Select(r => ToDto(r, r.LeaveType)).ToList(),
            Total = total,
            Page  = page,
            Limit = limit,
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the set of holiday dates within [start, end] for SandwichRuleEngine.
    /// Recurring holidays (IsRecurring=true) match by month+day regardless of year.
    /// </summary>
    private async Task<HashSet<DateOnly>> GetHolidaySetAsync(
        DateOnly start, DateOnly end, CancellationToken ct)
    {
        var allHolidays = await _db.Holidays
            .AsNoTracking()
            .ToListAsync(ct);

        var set = new HashSet<DateOnly>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (allHolidays.Any(h =>
                h.IsRecurring
                    ? h.Date.Month == d.Month && h.Date.Day == d.Day
                    : h.Date == d))
            {
                set.Add(d);
            }
        }

        return set;
    }

    private static LeaveRequestDto ToDto(LeaveRequest r, LeaveType lt) => new()
    {
        Id            = r.Id,
        EmployeeId    = r.EmployeeId,
        LeaveTypeId   = r.LeaveTypeId,
        LeaveTypeName = lt.Name,
        StartDate     = r.StartDate,
        EndDate       = r.EndDate,
        ComputedDays  = r.ComputedDays,
        Status        = r.Status.ToString(),
        IsRetroactive = r.IsRetroactive,
        Reason        = r.Reason,
        DocumentUrl   = r.DocumentUrl,
        CreatedAt     = r.CreatedAt,
        UpdatedAt     = r.UpdatedAt,
        ApprovalSteps = r.ApprovalSteps
            .OrderBy(s => s.StepNumber)
            .Select(s => new ApprovalStepDto
            {
                Id         = s.Id,
                StepNumber = s.StepNumber,
                ApproverId = s.ApproverId,
                Status     = s.Status.ToString(),
                ActedAt    = s.ActedAt,
                Comment    = s.Comment,
            }).ToList(),
    };
}
