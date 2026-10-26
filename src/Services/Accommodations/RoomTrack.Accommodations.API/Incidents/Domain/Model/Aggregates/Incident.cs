using RoomTrack.Incidents.Domain.Model.Entities;
using RoomTrack.Incidents.Domain.Model.Events;
using RoomTrack.Incidents.Domain.Model.Exceptions;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model;
using RoomTrack.Shared.Domain.Model.Events;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Incidents.Domain.Model.Aggregates;

/// <summary>
///     A damage or malfunction of a room reported by the hotel staff (US-08) and followed by maintenance until it is
///     resolved (US-09). Every change is recorded in its history.
/// </summary>
/// <remarks>
///     The room is a logical reference (id and hotel): the Rooms context owns it. A blocking incident
///     (<see cref="BlocksRoom"/>) takes the room out of service until it is resolved; the application service holds
///     and releases the room in the same transaction.
/// </remarks>
public class Incident : IHasDomainEvents, IHasConcurrencyVersion
{
    public const int TitleMinLength = 3;
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 1000;
    public const int ResolutionNotesMaxLength = 1000;
    public const int MaxEmailLength = 254;

    private readonly List<IEvent> _domainEvents = [];
    private readonly List<IncidentHistoryEntry> _history = [];
    private DateTimeOffset? _pendingReportAt;

    /// <summary>EF Core constructor.</summary>
    protected Incident()
    {
        Title = string.Empty;
    }

    private Incident(int hotelId, int roomId, string title, string? description, IncidentPriority priority, bool blocksRoom,
        IncidentActor reporter, DateTimeOffset now)
    {
        HotelId = hotelId;
        RoomId = roomId;
        Title = title;
        Description = description;
        Priority = priority;
        BlocksRoom = blocksRoom;
        Status = IncidentStatus.Pending;
        ReportedByUserId = reporter.UserId;
        ReportedByEmail = Truncate(reporter.Email);
        ReportedAt = now;
        Record(IncidentHistoryAction.Reported, $"Priority {priority}" + (blocksRoom ? ", blocks the room" : string.Empty), reporter, now);
        _pendingReportAt = now;
    }

    /// <summary>A staff member reports an incident of a room. It starts Pending and unassigned.</summary>
    public static Incident Report(int hotelId, int roomId, string title, string? description, IncidentPriority priority,
        bool blocksRoom, IncidentActor reporter, DateTimeOffset now) =>
        new(hotelId, roomId, NormalizeTitle(title), NormalizeDescription(description), priority, blocksRoom, reporter, now);

    public int Id { get; private set; }
    public int HotelId { get; private set; }
    public int RoomId { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public IncidentPriority Priority { get; private set; }
    public IncidentStatus Status { get; private set; }

    /// <summary>The room cannot be used until the incident is resolved.</summary>
    public bool BlocksRoom { get; private set; }

    public int ReportedByUserId { get; private set; }
    public string? ReportedByEmail { get; private set; }
    public DateTimeOffset ReportedAt { get; private set; }

    /// <summary>The maintenance staff member in charge, if any.</summary>
    public int? AssigneeUserId { get; private set; }
    public DateTimeOffset? AssignedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public int? ResolvedByUserId { get; private set; }
    public string? ResolutionNotes { get; private set; }

    /// <summary>When the administrators were alerted that this critical incident was not started in time.</summary>
    public DateTimeOffset? EscalatedAt { get; private set; }

    public bool IsOpen => Status != IncidentStatus.Resolved;

    /// <summary>
    ///     Optimistic concurrency: two changes made at once (e.g. a re-prioritization while maintenance resolves it)
    ///     cannot overwrite each other; the second one is refused.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>What happened to the incident, oldest first.</summary>
    public IReadOnlyList<IncidentHistoryEntry> History => _history.AsReadOnly();

    public IReadOnlyCollection<IEvent> DomainEvents
    {
        get
        {
            // The report event carries the generated id and is recorded once the incident has it.
            if (_pendingReportAt is { } at && Id > 0)
            {
                _domainEvents.Insert(0, new IncidentReportedEvent(Id, HotelId, RoomId, Priority, BlocksRoom, ReportedByUserId, at));
                _pendingReportAt = null;
            }
            return _domainEvents.AsReadOnly();
        }
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    ///     Assigns the incident to <paramref name="assigneeUserId"/> (whose role and hotel are checked by the
    ///     application service). Administrators assign anyone; other staff can only take an unassigned incident (or
    ///     one already theirs) for themselves. Assigning the current assignee again is a no-op.
    /// </summary>
    public void AssignTo(int assigneeUserId, IncidentActor actor, DateTimeOffset now)
    {
        EnsureOpen();
        if (!actor.IsAdministrator
            && (assigneeUserId != actor.UserId || (AssigneeUserId is { } current && current != actor.UserId)))
            throw new OperationNotAllowedException(IncidentErrorCodes.AssignNotAllowed,
                $"Incident {Id}: only an administrator can assign it to someone else or take it from its assignee.");
        if (AssigneeUserId == assigneeUserId) return;

        AssigneeUserId = assigneeUserId;
        AssignedAt = now;
        Record(IncidentHistoryAction.Assigned, $"Assigned to user {assigneeUserId}", actor, now);
        _domainEvents.Add(new IncidentAssignedEvent(Id, HotelId, assigneeUserId, actor.UserId, now));
    }

    /// <summary>The assignee (or an administrator) starts working on it: Pending → InProgress.</summary>
    public void Start(IncidentActor actor, DateTimeOffset now)
    {
        EnsureOpen();
        if (Status != IncidentStatus.Pending)
            throw new InvalidIncidentStatusTransitionException(Id, Status, "started");
        if (AssigneeUserId is null) throw new IncidentNotAssignedException(Id);
        EnsureCanWork(actor);

        Status = IncidentStatus.InProgress;
        StartedAt = now;
        Record(IncidentHistoryAction.Started, null, actor, now);
        _domainEvents.Add(new IncidentStartedEvent(Id, HotelId, ReportedByUserId, actor.UserId, now));
    }

    /// <summary>
    ///     The assignee (or an administrator) closes it with what was done: InProgress → Resolved.
    /// </summary>
    public void Resolve(string? resolutionNotes, IncidentActor actor, DateTimeOffset now)
    {
        EnsureOpen();
        if (Status != IncidentStatus.InProgress)
            throw new InvalidIncidentStatusTransitionException(Id, Status, "resolved");
        EnsureCanWork(actor);

        var notes = resolutionNotes?.Trim();
        if (string.IsNullOrEmpty(notes))
            throw new InvalidFieldException("resolutionNotes", IncidentErrorCodes.ResolutionNotesRequired,
                "Describe what was done to resolve the incident.");
        if (notes.Length > ResolutionNotesMaxLength)
            throw new InvalidFieldException("resolutionNotes", IncidentErrorCodes.ResolutionNotesLength,
                $"The resolution notes cannot exceed {ResolutionNotesMaxLength} characters.",
                new Dictionary<string, object?> { ["maxLength"] = ResolutionNotesMaxLength });

        Status = IncidentStatus.Resolved;
        ResolvedAt = now;
        ResolvedByUserId = actor.UserId;
        ResolutionNotes = notes;
        Record(IncidentHistoryAction.Resolved, notes, actor, now);
        _domainEvents.Add(new IncidentResolvedEvent(Id, HotelId, RoomId, BlocksRoom, ReportedByUserId, actor.UserId, now));
    }

    /// <summary>Re-classifies the urgency of an open incident (its assignee is notified). The same priority is a no-op.</summary>
    public void ChangePriority(IncidentPriority priority, IncidentActor actor, DateTimeOffset now)
    {
        EnsureOpen();
        if (priority == Priority) return;
        Record(IncidentHistoryAction.PriorityChanged, $"{Priority} → {priority}", actor, now);
        _domainEvents.Add(new IncidentPriorityChangedEvent(Id, HotelId, RoomId, AssigneeUserId, Priority, priority,
            actor.UserId, now));
        Priority = priority;
    }

    /// <summary>
    ///     A critical incident nobody started within <paramref name="threshold"/> of its report, not escalated yet.
    /// </summary>
    public bool IsEscalationDue(DateTimeOffset now, TimeSpan threshold) =>
        Priority == IncidentPriority.Critical && Status == IncidentStatus.Pending && EscalatedAt is null
        && now - ReportedAt >= threshold;

    /// <summary>Alerts the administrators of a late critical incident (once per incident).</summary>
    public bool Escalate(DateTimeOffset now, TimeSpan threshold)
    {
        if (!IsEscalationDue(now, threshold)) return false;
        EscalatedAt = now;
        _history.Add(new IncidentHistoryEntry(IncidentHistoryAction.Escalated, Status,
            $"Not started within {threshold.TotalMinutes:0} minutes", null, null, now));
        _domainEvents.Add(new IncidentEscalatedEvent(Id, HotelId, ReportedAt, now));
        return true;
    }

    private void EnsureOpen()
    {
        if (!IsOpen) throw new IncidentAlreadyResolvedException(Id);
    }

    private void EnsureCanWork(IncidentActor actor)
    {
        if (!actor.IsAdministrator && AssigneeUserId != actor.UserId) throw new NotIncidentAssigneeException(Id);
    }

    private void Record(IncidentHistoryAction action, string? detail, IncidentActor actor, DateTimeOffset now) =>
        _history.Add(new IncidentHistoryEntry(action, Status, detail, actor.UserId, actor.Email, now));

    private static string? Truncate(string? email) => email is { Length: > MaxEmailLength } ? email[..MaxEmailLength] : email;

    private static string NormalizeTitle(string? title)
    {
        var trimmed = title?.Trim() ?? string.Empty;
        if (trimmed.Length is < TitleMinLength or > TitleMaxLength)
            throw new InvalidFieldException("title", IncidentErrorCodes.TitleLength,
                $"The title must have between {TitleMinLength} and {TitleMaxLength} characters.",
                new Dictionary<string, object?> { ["minLength"] = TitleMinLength, ["maxLength"] = TitleMaxLength });
        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > DescriptionMaxLength)
            throw new InvalidFieldException("description", IncidentErrorCodes.DescriptionLength,
                $"The description cannot exceed {DescriptionMaxLength} characters.",
                new Dictionary<string, object?> { ["maxLength"] = DescriptionMaxLength });
        return trimmed;
    }
}
