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
    /// <summary>A step of the checklist was marked done (US-07).</summary>
    ChecklistItemChecked,
    /// <summary>A step of the checklist was marked as not done again.</summary>
    ChecklistItemUnchecked,
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

/// <summary>One step of a cleaning protocol (US-07).</summary>
/// <param name="Label">What to do, e.g. "Change the bed linen".</param>
/// <param name="IsRequired">The task cannot be completed until it is done.</param>
public sealed record ChecklistItemDefinition(string Label, bool IsRequired = true);

/// <summary>
///     A suggested starting protocol, returned by <c>GET /cleaning-tasks/checklists</c> to a hotel that configured none
///     (it is not applied until the hotel saves it).
/// </summary>
public static class CleaningChecklistDefaults
{
    public static IReadOnlyList<ChecklistItemDefinition> Standard { get; } =
    [
        new("Ventilar la habitación y retirar la basura"),
        new("Cambiar la ropa de cama y tender la cama"),
        new("Limpiar y desinfectar el baño"),
        new("Reponer toallas y amenities"),
        new("Limpiar superficies, espejos y polvo"),
        new("Aspirar o trapear el piso"),
        new("Revisar el minibar", IsRequired: false),
        new("Verificar luces, TV y aire acondicionado")
    ];
}
