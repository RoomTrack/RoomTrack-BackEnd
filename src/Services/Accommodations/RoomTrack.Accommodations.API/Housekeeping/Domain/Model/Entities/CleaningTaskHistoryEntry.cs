using BackendAwRoomTrack.API.Housekeeping.Domain.Model.ValueObjects;

namespace BackendAwRoomTrack.API.Housekeeping.Domain.Model.Entities;

/// <summary>
///     One line of the history of a cleaning task (what happened, who and when). Recorded by the <c>CleaningTask</c>
///     aggregate with the change itself; never modified.
/// </summary>
public class CleaningTaskHistoryEntry
{
    public const int MaxEmailLength = 254;
    public const int MaxDetailLength = 1000;

    /// <summary>EF Core constructor.</summary>
    protected CleaningTaskHistoryEntry() { }

    internal CleaningTaskHistoryEntry(CleaningTaskHistoryAction action, CleaningTaskStatus status, string? detail,
        int? incidentId, int? actorUserId, string? actorEmail, DateTimeOffset occurredAt)
    {
        Action = action;
        Status = status;
        Detail = detail is { Length: > MaxDetailLength } ? detail[..MaxDetailLength] : detail;
        IncidentId = incidentId;
        ActorUserId = actorUserId;
        ActorEmail = actorEmail is { Length: > MaxEmailLength } ? actorEmail[..MaxEmailLength] : actorEmail;
        OccurredAt = occurredAt;
    }

    public long Id { get; private set; }
    public int CleaningTaskId { get; private set; }
    public CleaningTaskHistoryAction Action { get; private set; }

    /// <summary>The status of the task after this line.</summary>
    public CleaningTaskStatus Status { get; private set; }

    /// <summary>What changed, e.g. the new priority, the assignee or the reason of a cancellation.</summary>
    public string? Detail { get; private set; }

    /// <summary>The incident reported for maintenance (only for <see cref="CleaningTaskHistoryAction.ImpedimentReported"/>).</summary>
    public int? IncidentId { get; private set; }

    /// <summary>Who did it (null for the system, e.g. a task created when the room went to Cleaning).</summary>
    public int? ActorUserId { get; private set; }

    /// <summary>Their e-mail when it happened.</summary>
    public string? ActorEmail { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
