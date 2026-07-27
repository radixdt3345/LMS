using LMS.Domain.Entities;
using Xunit;

namespace LMS.Tests.Unit.People;

/// <summary>
/// Unit tests for the Department entity — PEOPLE-DB-001.
/// UT-14: entity creation with required fields.
/// UT-15: IsActive defaults to true.
/// UT-16: soft delete via IsActive flag.
/// UT-17: Description is optional.
/// </summary>
[Trait("Category", "Unit")]
public class DepartmentEntityTests
{
    // ── UT-14: department can be created with required fields ─────────────────

    [Fact]
    public void Department_CanBeCreatedWithRequiredFields()
    {
        var now = DateTime.UtcNow;
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = "Engineering",
            CreatedAt = now,
            UpdatedAt = now,
        };

        Assert.Equal("Engineering", dept.Name);
        Assert.NotEqual(Guid.Empty, dept.Id);
        Assert.Equal(now, dept.CreatedAt);
        Assert.Equal(now, dept.UpdatedAt);
    }

    // ── UT-15: IsActive defaults to true ─────────────────────────────────────

    [Fact]
    public void Department_IsActiveDefaultsToTrue()
    {
        var dept = new Department { Name = "HR" };
        Assert.True(dept.IsActive);
    }

    // ── UT-16: soft delete via IsActive = false ───────────────────────────────

    [Fact]
    public void Department_CanBeSoftDeleted()
    {
        var dept = new Department
        {
            Id = Guid.NewGuid(),
            Name = "Legal",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Soft delete — no physical row removed
        dept.IsActive = false;
        dept.UpdatedAt = DateTime.UtcNow;

        Assert.False(dept.IsActive);
    }

    // ── UT-17: Description is optional ───────────────────────────────────────

    [Fact]
    public void Department_DescriptionIsOptional()
    {
        var dept = new Department { Name = "Finance" };
        Assert.Null(dept.Description);

        dept.Description = "Finance and Accounting";
        Assert.Equal("Finance and Accounting", dept.Description);
    }

    // ── Name length guard: entity allows 100-char names ───────────────────────

    [Fact]
    public void Department_NameCanBe100CharsLong()
    {
        var longName = new string('A', 100);
        var dept = new Department { Name = longName };
        Assert.Equal(100, dept.Name.Length);
    }
}
