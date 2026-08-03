namespace LMS.Domain.Entities;

/// <summary>
/// In-app notification delivered to a specific user.
/// Stores title, body, read-state, and an optional resource link.
/// </summary>
public class Notification
{
    public Guid Id { get; set; }

    /// <summary>Target user who receives this notification.</summary>
    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>False until the user explicitly marks it read.</summary>
    public bool IsRead { get; set; } = false;

    /// <summary>Optional resource type (e.g. "LeaveRequest", "CompOffRequest").</summary>
    public string? ResourceType { get; set; }

    /// <summary>Optional PK of the linked resource for deep-linking.</summary>
    public Guid? ResourceId { get; set; }

    /// <summary>UTC timestamp of creation — never modified.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp when the user read the notification (null = unread).</summary>
    public DateTime? ReadAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
