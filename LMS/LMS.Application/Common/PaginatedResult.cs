namespace LMS.Application.Common;

/// <summary>
/// Generic paginated result wrapper — returned by service list methods.
/// Serialises to { items, total, page, limit } matching the API contract.
/// </summary>
/// <typeparam name="T">DTO type for each item.</typeparam>
public record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int Limit);
