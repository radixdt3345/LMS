using LMS.Domain.Entities;
using Xunit;

namespace LMS.Tests.Unit.People;

[Trait("Category", "Unit")]
public class DepartmentEntityTests
{
    [Fact]
    public void Department_CanBeCreatedWithRequiredFields()
    {
        var now = DateTime.UtcNow;
        var dept = new Department { Id = Guid.NewGuid(), Name = "Engineering", CreatedAt = now, UpdatedAt = now };
        Assert.Equal("Engineering", dept.Name);
        Assert.NotEqual(Guid.Empty, dept.Id);
    }

    [Fact]
    public void Department_IsActiveDefaultsToTrue()
    {
        var dept = new Department { Name = "HR" };
        Assert.True(dept.IsActive);
    }

    [Fact]
    public void Department_CanBeSoftDeleted()
    {
        var dept = new Department { Id = Guid.NewGuid(), Name = "Legal", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dept.IsActive = false;
        Assert.False(dept.IsActive);
    }

    [Fact]
    public void Department_DescriptionIsOptional()
    {
        var dept = new Department { Name = "Finance" };
        Assert.Null(dept.Description);
        dept.Description = "Finance and Accounting";
        Assert.Equal("Finance and Accounting", dept.Description);
    }

    [Fact]
    public void Department_NameCanBe100CharsLong()
    {
        var longName = new string('A', 100);
        var dept = new Department { Name = longName };
        Assert.Equal(100, dept.Name.Length);
    }
}
