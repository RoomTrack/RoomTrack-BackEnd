using RoomTrack.GuestRequests.Domain.Model.ValueObjects;

namespace RoomTrack.GuestRequests.Domain.Model.Entities;

/// <summary>One line of the history of a guest request (what happened, who and when). Never modified.</summary>
public class GuestRequestHistoryEntry
{
    public const int MaxEmailLength = 254;
    public const int MaxDetailLength = 1000;

    /// <summary>EF Core constructor.</summary>
    protected GuestRequestHistoryEntry() { }

    internal GuestRequestHistoryEntry(GuestRequestHistoryAction action, GuestRequestStatus status, string? detail,
        int? actorUserId, string? actorEmail, DateTimeOffset occurredAt)
    {
        Action = action;
        Status = status;
        Detail = detail is { Length: > MaxDetailLength } ? detail[..MaxDetailLength] : detail;
        ActorUserId = actorUserId;
        ActorEmail = actorEmail is { Length: > MaxEmailLength } ? actorEmail[..MaxEmailLength] : actorEmail;
        OccurredAt = occurredAt;
    }

    public long Id { get; private set; }
    public int GuestRequestId { get; private set; }
    public GuestRequestHistoryAction Action { get; private set; }

    /// <summary>The status of the request after this line.</summary>
    public GuestRequestStatus Status { get; private set; }

    /// <summary>What changed, e.g. the area it was routed to or the resolution.</summary>
    public string? Detail { get; private set; }

    public int? ActorUserId { get; private set; }
    public string? ActorEmail { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
