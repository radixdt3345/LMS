using LMS.Application.Interfaces;
using LMS.Domain.Common;
using LMS.Domain.Entities;
using LMS.Domain.Enums;
using LMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Infrastructure.Services;

/// <summary>
/// Implements approval step routing for leave requests.
///
/// No-manager rule (UT-53, IT-40):
/// - manager_id IS NOT NULL → Step 1 = manager, Step 2 = HR Admin.
/// - manager_id IS NULL     → Step 1 = HR Admin ONLY; L2 unconditionally skipped.
/// </summary>
public class ApprovalService : IApprovalService
{
    private readonly LmsDbContext _db;

    public ApprovalService(LmsDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> CreateApprovalStepsAsync(
        Guid leaveRequestId, User employee, CancellationToken ct = default)
    {
        // Locate the first active HR Admin — required for all routing paths.
        var hrAdmin = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Role == UserRole.HRAdmin && u.IsActive, ct);

        if (hrAdmin is null)
            return Result<bool>.Failure(
                "No active HR Admin found. Cannot route approval.", 500);

        var now   = DateTime.UtcNow;
        var steps = new List<ApprovalStep>();

        if (employee.ManagerId is not null)
        {
            // Two-step: L1 = direct manager, L2 = HR Admin.
            steps.Add(new ApprovalStep
            {
                Id             = Guid.NewGuid(),
                LeaveRequestId = leaveRequestId,
                StepNumber     = 1,
                ApproverId     = employee.ManagerId.Value,
                Status         = ApprovalStepStatus.Pending,
                CreatedAt      = now,
            });
            steps.Add(new ApprovalStep
            {
                Id             = Guid.NewGuid(),
                LeaveRequestId = leaveRequestId,
                StepNumber     = 2,
                ApproverId     = hrAdmin.Id,
                Status         = ApprovalStepStatus.Pending,
                CreatedAt      = now,
            });
        }
        else
        {
            // No manager — single step: HR Admin is L1.
            // L2 is unconditionally skipped even for retroactive requests (UT-53).
            steps.Add(new ApprovalStep
            {
                Id             = Guid.NewGuid(),
                LeaveRequestId = leaveRequestId,
                StepNumber     = 1,
                ApproverId     = hrAdmin.Id,
                Status         = ApprovalStepStatus.Pending,
                CreatedAt      = now,
            });
        }

        _db.ApprovalSteps.AddRange(steps);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
