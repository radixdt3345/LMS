namespace LMS.Domain.Common;

/// <summary>
/// Generic paginated result wrapper.
/// API responses include total, page, and limit alongside the items collection.
/// </summary>
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
}
