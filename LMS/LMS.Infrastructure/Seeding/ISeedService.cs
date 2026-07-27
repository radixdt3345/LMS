namespace LMS.Infrastructure.Seeding;

/// <summary>Idempotent startup data seeder.</summary>
public interface ISeedService
{
    Task SeedAsync(CancellationToken ct = default);
}
