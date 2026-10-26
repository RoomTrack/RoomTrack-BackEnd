using RoomTrack.Housekeeping.Domain.Model.Entities;
using RoomTrack.Housekeeping.Domain.Model.Events;
using RoomTrack.Housekeeping.Domain.Model.Exceptions;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model;
using RoomTrack.Shared.Domain.Model.Events;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Housekeeping.Domain.Model.Aggregates;

/// <summary>
///     The cleaning of a room, assigned to a housekeeping staff member (US-05) who updates it from their task list
///     (US-06). Every change is recorded in its history.
/// </summary>
/// <remarks>
///     The room is a logical reference (id and hotel): the Rooms context owns it. Starting the task takes the room to
///     Cleaning and completing it makes the room Available; the application service changes the room in the same
///     transaction. Starting an already started task, or completing an already completed one, is a no-op so a client
///     replaying its offline queue does not fail.
/// </remarks>
public class CleaningTask : IHasDomainEvents, IHasConcurrencyVersion
{
    public const int NotesMaxLength = 500;
    public const int CancellationReasonMaxLength = 500;
    public const int MaxEmailLength = 254;

    private readonly List<IEvent> _domainEvents = [];
    private readonly List<CleaningTaskHistoryEntry> _history = [];

    // Events recorded before the task has its generated id (an automatic assignment at creation).
    private readonly List<Func<IEvent>> _eventsAwaitingId = [];

    /// <summary>EF Core constructor.</summary>
    protected CleaningTask() { }

    private CleaningTask(int hotelId, int roomId, CleaningTaskPriority priority, string? notes, CleaningTaskActor? creator,
        DateTimeOffset now)
    {
        HotelId = hotelId;
        RoomId = roomId;
        Priority = priority;
        Notes = notes;
        Status = CleaningTaskStatus.Pending;
        CreatedByUserId = creator?.UserId;
        CreatedAt = now;
        Record(CleaningTaskHistoryAction.Created, $"Priority {priority}" + (creator is null ? ", the room went to Cleaning" : string.Empty),
            creator, now);
    }

    /// <summary>The front desk creates a cleaning task for a room. It starts Pending and unassigned.</summary>
    public static CleaningTask Create(int hotelId, int roomId, CleaningTaskPriority priority, string? notes,
        CleaningTaskActor creator, DateTimeOffset now)
    {
        if (!creator.CanManage) throw new CleaningTaskManageNotAllowedException("create");
        return new CleaningTask(hotelId, roomId, priority, NormalizeNotes(notes), creator, now);
    }

    /// <summary>The system creates the task of a room that went to Cleaning (a staff change, a resolved incident).</summary>
    public static CleaningTask CreateForRoomToClean(int hotelId, int roomId, DateTimeOffset now) =>
        new(hotelId, roomId, CleaningTaskPriority.Normal, null, null, now);

    public int Id { get; private set; }
    public int HotelId { get; private set; }
    public int RoomId { get; private set; }
    public CleaningTaskPriority Priority { get; private set; }
    public CleaningTaskStatus Status { get; private set; }

    /// <summary>Instructions for housekeeping, e.g. "Guest arrives at 15:00, add an extra bed".</summary>
    public string? Notes { get; private set; }

    /// <summary>Who created it (null when the system did).</summary>
    public int? CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The housekeeping staff member in charge, if any.</summary>
    public int? AssigneeUserId { get; private set; }
    public DateTimeOffset? AssignedAt { get; private set; }

    /// <summary>The last assignment was made by the automatic assignment (least workload).</summary>
    public bool AssignedAutomatically { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int? CompletedByUserId { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public bool IsOpen => Status is CleaningTaskStatus.Pending or CleaningTaskStatus.InProgress;

    /// <summary>
    ///     Optimistic concurrency: a reception change and a housekeeper's update made at once cannot overwrite each
    ///     other (e.g. a cancelled task completed by its assignee); the second one is refused.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>What happened to the task, oldest first.</summary>
    public IReadOnlyList<CleaningTaskHistoryEntry> History => _history.AsReadOnly();

    /// <summary>The incidents reported for maintenance while cleaning, oldest first.</summary>
    public IReadOnlyList<int> ImpedimentIncidentIds =>
        _history.Where(entry => entry.IncidentId is not null).Select(entry => entry.IncidentId!.Value).ToList();

    public IReadOnlyCollection<IEvent> DomainEvents
    {
        get
        {
            if (Id > 0 && _eventsAwaitingId.Count > 0)
            {
                _domainEvents.InsertRange(0, _eventsAwaitingId.Select(create => create()));
                _eventsAwaitingId.Clear();
            }
            return _domainEvents.AsReadOnly();
        }
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
        _eventsAwaitingId.Clear();
    }

    /// <summary>
    ///     Assigns the task to <paramref name="assigneeUserId"/> (whose role and hotel are checked by the application
    ///     service). The front desk assigns and reassigns to anyone; housekeeping can only take an unassigned task (or
    ///     one already theirs) for themselves. Assigning the current assignee again is a no-op.
    /// </summary>
    public void AssignTo(int assigneeUserId, CleaningTaskActor actor, DateTimeOffset now)
    {
        EnsureOpen();
        if (!actor.CanManage
            && (assigneeUserId != actor.UserId || (AssigneeUserId is { } current && current != actor.UserId)))
            throw new OperationNotAllowedException(CleaningTaskErrorCodes.AssignNotAllowed,
                $"Cleaning task {Id}: only reception or an administrator can assign it to someone else or take it from its assignee.");
        Assign(assigneeUserId, actor, automatically: false, now);
    }

    /// <summary>
    ///     Assigns the task to the housekeeping staff member chosen by the automatic assignment (the least loaded one).
    ///     <paramref name="actor"/> is null when the system assigns a task it created.
    /// </summary>
    public void AssignAutomatically(int assigneeUserId, CleaningTaskActor? actor, DateTimeOffset now)
    {
        EnsureOpen();
        if (actor is { CanManage: false }) throw new CleaningTaskManageNotAllowedException("automatically assign");
        Assign(assigneeUserId, actor, automatically: true, now);
    }

    /// <summary>
    ///     The assignee (or an administrator) starts cleaning: Pending → InProgress. Returns false when it was already
    ///     in progress (nothing changes).
    /// </summary>
    public bool Start(CleaningTaskActor actor, DateTimeOffset now)
    {
        EnsureCanWork(actor);
        if (Status == CleaningTaskStatus.InProgress) return false;
        EnsureOpen();
        if (AssigneeUserId is null) throw new CleaningTaskNotAssignedException(Id);

        Status = CleaningTaskStatus.InProgress;
        StartedAt = now;
        Record(CleaningTaskHistoryAction.Started, null, actor, now);
        return true;
    }

    /// <summary>
    ///     The assignee (or an administrator) finished cleaning: InProgress → Completed. Returns false when it was
    ///     already completed (nothing changes).
    /// </summary>
    public bool Complete(CleaningTaskActor actor, DateTimeOffset now)
    {
        EnsureCanWork(actor);
        if (Status == CleaningTaskStatus.Completed) return false;
        EnsureOpen();
        if (Status != CleaningTaskStatus.InProgress)
            throw new InvalidCleaningTaskStatusTransitionException(Id, Status, "completed");

        Status = CleaningTaskStatus.Completed;
        CompletedAt = now;
        CompletedByUserId = actor.UserId;
        Record(CleaningTaskHistoryAction.Completed, null, actor, now);
        _domainEvents.Add(new CleaningTaskCompletedEvent(Id, HotelId, RoomId, actor.UserId, now));
        return true;
    }

    /// <summary>
    ///     The assignee (or an administrator) found a damage while cleaning: it was reported as incident
    ///     <paramref name="incidentId"/> for maintenance. The task stays as it is; when the incident blocks the room, the
    ///     task cannot be completed until the incident is resolved.
    /// </summary>
    public void RecordImpediment(int incidentId, string title, CleaningTaskActor actor, DateTimeOffset now)
    {
        EnsureOpen();
        EnsureCanWork(actor);
        _history.Add(new CleaningTaskHistoryEntry(CleaningTaskHistoryAction.ImpedimentReported, Status, title, incidentId,
            actor.UserId, actor.Email, now));
    }

    /// <summary>Re-classifies the urgency of an open task (its assignee is notified). The same priority is a no-op.</summary>
    public void ChangePriority(CleaningTaskPriority priority, CleaningTaskActor actor, DateTimeOffset now)
    {
        if (!actor.CanManage) throw new CleaningTaskManageNotAllowedException("re-prioritize");
        EnsureOpen();
        if (priority == Priority) return;
        Record(CleaningTaskHistoryAction.PriorityChanged, $"{Priority} → {priority}", actor, now);
        _domainEvents.Add(new CleaningTaskPriorityChangedEvent(Id, HotelId, RoomId, AssigneeUserId, Priority, priority,
            actor.UserId, now));
        Priority = priority;
    }

    /// <summary>
    ///     The front desk cancels an open task (e.g. created by mistake). <paramref name="actor"/> is null when the
    ///     system cancels it because the room was made Available without it.
    /// </summary>
    public void Cancel(string? reason, CleaningTaskActor? actor, DateTimeOffset now)
    {
        if (actor is { CanManage: false }) throw new CleaningTaskManageNotAllowedException("cancel");
        EnsureOpen();
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed)) trimmed = null;
        else if (trimmed.Length > CancellationReasonMaxLength)
            throw new InvalidFieldException("reason", CleaningTaskErrorCodes.CancellationReasonLength,
                $"The reason cannot exceed {CancellationReasonMaxLength} characters.",
                new Dictionary<string, object?> { ["maxLength"] = CancellationReasonMaxLength });

        Status = CleaningTaskStatus.Cancelled;
        CancelledAt = now;
        CancellationReason = trimmed;
        Record(CleaningTaskHistoryAction.Cancelled, trimmed, actor, now);
    }

    private void Assign(int assigneeUserId, CleaningTaskActor? actor, bool automatically, DateTimeOffset now)
    {
        if (AssigneeUserId == assigneeUserId) return;

        var previous = AssigneeUserId;
        AssigneeUserId = assigneeUserId;
        AssignedAt = now;
        AssignedAutomatically = automatically;
        Record(previous is null ? CleaningTaskHistoryAction.Assigned : CleaningTaskHistoryAction.Reassigned,
            $"Assigned to user {assigneeUserId}" + (automatically ? " (least workload)" : string.Empty)
                                                 + (previous is { } from ? $", was user {from}" : string.Empty),
            actor, now);

        var assignedBy = actor?.UserId;
        if (Id > 0)
            _domainEvents.Add(new CleaningTaskAssignedEvent(Id, HotelId, RoomId, assigneeUserId, previous, assignedBy, now));
        else
            _eventsAwaitingId.Add(() => new CleaningTaskAssignedEvent(Id, HotelId, RoomId, assigneeUserId, previous, assignedBy, now));
    }

    private void EnsureOpen()
    {
        if (!IsOpen) throw new CleaningTaskClosedException(Id, Status);
    }

    private void EnsureCanWork(CleaningTaskActor actor)
    {
        if (!actor.IsAdministrator && AssigneeUserId != actor.UserId)
        {
            // A housekeeper starting an unassigned task takes it first (application service); anyone else is refused.
            if (AssigneeUserId is null && Status == CleaningTaskStatus.Pending) throw new CleaningTaskNotAssignedException(Id);
            throw new NotCleaningTaskAssigneeException(Id);
        }
    }

    private void Record(CleaningTaskHistoryAction action, string? detail, CleaningTaskActor? actor, DateTimeOffset now) =>
        _history.Add(new CleaningTaskHistoryEntry(action, Status, detail, null, actor?.UserId, Truncate(actor?.Email), now));

    private static string? Truncate(string? email) => email is { Length: > MaxEmailLength } ? email[..MaxEmailLength] : email;

    private static string? NormalizeNotes(string? notes)
    {
        var trimmed = notes?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > NotesMaxLength)
            throw new InvalidFieldException("notes", CleaningTaskErrorCodes.NotesLength,
                $"The notes cannot exceed {NotesMaxLength} characters.",
                new Dictionary<string, object?> { ["maxLength"] = NotesMaxLength });
        return trimmed;
    }
}
