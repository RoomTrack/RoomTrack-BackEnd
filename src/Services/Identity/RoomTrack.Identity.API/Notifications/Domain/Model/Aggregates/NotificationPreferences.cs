using RoomTrack.Notifications.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Contracts.Messaging;

namespace RoomTrack.Notifications.Domain.Model.Aggregates;

/// <summary>
///     The kinds of push notifications (<see cref="PushCategories"/>) a user turned off (US-22, scenario 3). Every kind
///     is on until the user turns it off, so a user without preferences gets them all.
/// </summary>
public class NotificationPreferences
{
    /// <summary>EF Core constructor.</summary>
    protected NotificationPreferences() { }

    public NotificationPreferences(int userId)
    {
        UserId = userId;
    }

    public int UserId { get; private set; }

    /// <summary>The kinds of notification the user does not want, sorted and comma-separated (stored as is).</summary>
    public string DisabledCategoryCodes { get; private set; } = string.Empty;

    /// <summary>The kinds of notification the user does not want, sorted.</summary>
    public IReadOnlyList<string> DisabledCategories =>
        DisabledCategoryCodes.Split(',', StringSplitOptions.RemoveEmptyEntries);

    public DateTimeOffset? UpdatedAt { get; private set; }

    public bool IsEnabled(string category) => !DisabledCategories.Contains(category);

    /// <summary>Replaces the kinds of notification turned off. Unknown kinds are refused.</summary>
    public void Disable(IEnumerable<string> categories, DateTimeOffset now)
    {
        var disabled = categories.Select(category => category?.Trim().ToLowerInvariant() ?? string.Empty)
            .Distinct().Order(StringComparer.Ordinal).ToList();
        var unknown = disabled.Where(category => !PushCategories.All.Contains(category)).ToList();
        if (unknown.Count > 0)
            throw new InvalidFieldException("disabledCategories", NotificationErrorCodes.CategoryUnknown,
                $"Unknown notification kinds: {string.Join(", ", unknown)}. Valid: {string.Join(", ", PushCategories.All)}.",
                new Dictionary<string, object?> { ["validCategories"] = PushCategories.All });

        DisabledCategoryCodes = string.Join(',', disabled);
        UpdatedAt = now;
    }
}
