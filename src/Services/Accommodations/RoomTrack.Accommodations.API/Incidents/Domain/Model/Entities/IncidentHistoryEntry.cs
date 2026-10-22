using BackendAwRoomTrack.API.Incidents.Domain.Model.ValueObjects;

namespace BackendAwRoomTrack.API.Incidents.Domain.Model.Entities;

/// <summary>
///     One line of the history of an incident (what happened, who and when). Recorded by the <c>Incident</c>
///     aggregate with the change itself; never modified.
/// </summary>
public class IncidentHistoryEntry
{
    public const int MaxEmailLength = 254;
    public const int MaxDetailLength = 1000;

    /// <summary>EF Core constructor.</summary>
    protected IncidentHistoryEntry() { }

    internal IncidentHistoryEntry(IncidentHistoryAction action, IncidentStatus status, string? detail, int? actorUserId,
        string? actorEmail, DateTimeOffset occurredAt)
    {
        Action = action;
        Status = status;
        Detail = detail is { Length: > MaxDetailLength } ? detail[..MaxDetailLength] : detail;
        ActorUserId = actorUserId;
        ActorEmail = actorEmail is { Length: > MaxEmailLength } ? actorEmail[..MaxEmailLength] : actorEmail;
        OccurredAt = occurredAt;
    }

    public long Id { get; private set; }
    public int IncidentId { get; private set; }
    public IncidentHistoryAction Action { get; private set; }

    /// <summary>The status of the incident after this line.</summary>
    public IncidentStatus Status { get; private set; }

    /// <summary>What changed, e.g. the new priority, the assignee or the resolution notes.</summary>
    public string? Detail { get; private set; }

    /// <summary>Who did it (null for the system, e.g. an escalation).</summary>
    public int? ActorUserId { get; private set; }

    /// <summary>Their e-mail when it happened.</summary>
    public string? ActorEmail { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
