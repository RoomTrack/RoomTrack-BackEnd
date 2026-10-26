namespace RoomTrack.Housekeeping.Domain.Model.ValueObjects;

/// <summary>How urgent a cleaning task is. The housekeeping list is ordered by it (Urgent first).</summary>
public enum CleaningTaskPriority
{
    Low,
    Normal,
    High,
    /// <summary>E.g. a guest is about to check in to the room.</summary>
    Urgent
}

/// <summary>
///     Lifecycle of a cleaning task: Pending (created, maybe assigned) → InProgress (the room is being cleaned) →
///     Completed (the room is Available). An open task can be Cancelled by the front desk.
/// </summary>
public enum CleaningTaskStatus
{
    Pending,
    InProgress,
    Completed,
    Cancelled
}

/// <summary>What one line of the cleaning task history records.</summary>
public enum CleaningTaskHistoryAction
{
    Created,
    Assigned,
    Reassigned,
    Started,
    PriorityChanged,
    /// <summary>A damage found while cleaning was reported as an incident for maintenance.</summary>
    ImpedimentReported,
    Completed,
    Cancelled
}

/// <summary>What the actor of a cleaning task may do besides working on their own tasks.</summary>
public enum CleaningTaskRole
{
    /// <summary>Housekeeping staff: works on their own tasks and may take an unassigned one.</summary>
    Housekeeper,
    /// <summary>Reception: creates, assigns, reassigns, re-prioritizes and cancels tasks.</summary>
    FrontDesk,
    /// <summary>Hotel or chain administrator: everything the front desk does, and works on anyone's task.</summary>
    Administrator
}

/// <summary>Who acts on a cleaning task.</summary>
/// <param name="UserId">IAM user id.</param>
/// <param name="Email">Their e-mail (kept in the history).</param>
/// <param name="Role">What they may do.</param>
public sealed record CleaningTaskActor(int UserId, string? Email, CleaningTaskRole Role)
{
    /// <summary>Creates, assigns to anyone, re-prioritizes and cancels tasks.</summary>
    public bool CanManage => Role is CleaningTaskRole.FrontDesk or CleaningTaskRole.Administrator;

    /// <summary>Works on tasks assigned to someone else.</summary>
    public bool IsAdministrator => Role == CleaningTaskRole.Administrator;
}
