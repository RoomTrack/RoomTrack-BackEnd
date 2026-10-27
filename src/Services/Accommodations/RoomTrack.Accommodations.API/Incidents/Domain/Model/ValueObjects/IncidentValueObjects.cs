namespace RoomTrack.Incidents.Domain.Model.ValueObjects;

/// <summary>How urgent an incident is. The maintenance list is ordered by it (Critical first).</summary>
public enum IncidentPriority
{
    Low,
    Medium,
    High,
    /// <summary>Escalated to the hotel administrators when nobody starts it in time.</summary>
    Critical
}

/// <summary>
///     Lifecycle of an incident: Pending (reported, maybe assigned) → InProgress (the assignee works on it) →
///     Resolved (closed with resolution notes).
/// </summary>
public enum IncidentStatus
{
    Pending,
    InProgress,
    Resolved
}

/// <summary>What one line of the incident history records.</summary>
public enum IncidentHistoryAction
{
    Reported,
    Assigned,
    Started,
    PriorityChanged,
    Escalated,
    Resolved
}

/// <summary>Who acts on an incident.</summary>
/// <param name="UserId">IAM user id.</param>
/// <param name="Email">Their e-mail (kept in the history).</param>
/// <param name="IsAdministrator">
///     A hotel or chain administrator: may assign the incident to anyone and work on incidents assigned to others.
/// </param>
public sealed record IncidentActor(int UserId, string? Email, bool IsAdministrator);
