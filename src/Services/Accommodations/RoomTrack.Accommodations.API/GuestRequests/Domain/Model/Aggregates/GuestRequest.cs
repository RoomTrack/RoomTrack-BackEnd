using RoomTrack.GuestRequests.Domain.Model.Entities;
using RoomTrack.GuestRequests.Domain.Model.Events;
using RoomTrack.GuestRequests.Domain.Model.Exceptions;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model;
using RoomTrack.Shared.Domain.Model.Events;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.GuestRequests.Domain.Model.Aggregates;

/// <summary>
///     Something a guest needs during their stay (towels, a repair, a question...): sent by the guest from the app
///     (US-19) or registered by the front desk for them (US-11), attended by an operational area and followed until it
///     is resolved. Every change is recorded in its history.
/// </summary>
/// <remarks>
///     A guest's request arrives at the front desk unless the guest chose the area; the front desk routes it to the
///     area that attends it. The room is a logical reference (the Rooms context owns it). The guest is notified of
///     every change of status, and gets an estimated attention time when it is registered.
/// </remarks>
public class GuestRequest : IHasDomainEvents, IHasConcurrencyVersion
{
    public const int TitleMinLength = 3;
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 1000;
    public const int NoteMaxLength = 500;
    public const int MaxEmailLength = 254;

    private readonly List<IEvent> _domainEvents = [];
    private readonly List<GuestRequestHistoryEntry> _history = [];
    private bool _creationPending;

    /// <summary>EF Core constructor.</summary>
    protected GuestRequest()
    {
        Title = string.Empty;
    }

    private GuestRequest(int hotelId, int roomId, int? bookingId, int? guestUserId, string title, string? description,
        ServiceArea area, GuestRequestActor creator, DateTimeOffset estimatedAttentionBy, DateTimeOffset now)
    {
        HotelId = hotelId;
        RoomId = roomId;
        BookingId = bookingId;
        GuestUserId = guestUserId;
        Title = NormalizeTitle(title);
        Description = Normalize(description, DescriptionMaxLength, "description", GuestRequestErrorCodes.DescriptionLength);
        Area = area;
        Status = GuestRequestStatus.Pending;
        CreatedByUserId = creator.UserId;
        CreatedByGuest = creator.IsGuest;
        CreatedAt = now;
        EstimatedAttentionBy = estimatedAttentionBy;
        Record(GuestRequestHistoryAction.Created, $"For {area}", creator, now);
        _creationPending = true;
    }

    /// <summary>The guest sends a request during their stay (it arrives at the front desk unless they chose the area).</summary>
    public static GuestRequest SendByGuest(int hotelId, int roomId, int bookingId, string title, string? description,
        ServiceArea? area, GuestRequestActor guest, DateTimeOffset estimatedAttentionBy, DateTimeOffset now)
    {
        if (!guest.IsGuest) throw new GuestRequestNotAllowedException("send as a guest");
        return new GuestRequest(hotelId, roomId, bookingId, guest.UserId, title, description, area ?? ServiceArea.Reception,
            guest, estimatedAttentionBy, now);
    }

    /// <summary>The front desk registers what a guest asked for (in person or by phone) for the area that attends it.</summary>
    public static GuestRequest RegisterByFrontDesk(int hotelId, int roomId, int? bookingId, int? guestUserId, string title,
        string? description, ServiceArea area, GuestRequestActor frontDesk, DateTimeOffset estimatedAttentionBy, DateTimeOffset now)
    {
        if (!frontDesk.CanManage) throw new GuestRequestNotAllowedException("register");
        return new GuestRequest(hotelId, roomId, bookingId, guestUserId, title, description, area, frontDesk, estimatedAttentionBy, now);
    }

    public int Id { get; private set; }
    public int HotelId { get; private set; }
    public int RoomId { get; private set; }

    /// <summary>The stay it belongs to (null when the front desk registered it without one).</summary>
    public int? BookingId { get; private set; }

    /// <summary>The guest's IAM account, when they have one: they follow the request in the app.</summary>
    public int? GuestUserId { get; private set; }

    public string Title { get; private set; }
    public string? Description { get; private set; }
    public ServiceArea Area { get; private set; }
    public GuestRequestStatus Status { get; private set; }
    public int CreatedByUserId { get; private set; }
    public bool CreatedByGuest { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the guest was told it would be attended (US-19, "tiempo estimado de atención").</summary>
    public DateTimeOffset EstimatedAttentionBy { get; private set; }

    public int? AssigneeUserId { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>How it was resolved, or why it was cancelled.</summary>
    public string? ClosingNote { get; private set; }

    public int Version { get; private set; }

    public bool IsOpen => Status is GuestRequestStatus.Pending or GuestRequestStatus.InProgress;

    public IReadOnlyList<GuestRequestHistoryEntry> History => _history.AsReadOnly();

    public IReadOnlyCollection<IEvent> DomainEvents
    {
        get
        {
            if (_creationPending && Id > 0)
            {
                _creationPending = false;
                _domainEvents.Insert(0, new GuestRequestCreatedEvent(Id, HotelId, RoomId, Area, GuestUserId, CreatedByUserId,
                    EstimatedAttentionBy, CreatedAt));
            }
            return _domainEvents.AsReadOnly();
        }
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>The guest of the request (their own requests only).</summary>
    public bool BelongsTo(GuestRequestActor actor) => actor.IsGuest && GuestUserId == actor.UserId;

    /// <summary>The front desk derives the request to the area that attends it (US-11). The same area is a no-op.</summary>
    public void RouteTo(ServiceArea area, GuestRequestActor actor, DateTimeOffset estimatedAttentionBy, DateTimeOffset now)
    {
        if (!actor.CanManage) throw new GuestRequestNotAllowedException("route");
        EnsureOpen();
        if (area == Area) return;

        var from = Area;
        Area = area;
        AssigneeUserId = null;
        EstimatedAttentionBy = estimatedAttentionBy;
        Record(GuestRequestHistoryAction.Routed, $"{from} → {area}", actor, now);
        _domainEvents.Add(new GuestRequestRoutedEvent(Id, HotelId, RoomId, from, area, actor.UserId, now));
    }

    /// <summary>
    ///     A staff member takes charge: the front desk assigns anyone of the area (checked by the application service);
    ///     a staff member of the area can take an unassigned request for themselves. The same assignee is a no-op.
    /// </summary>
    public void AssignTo(int assigneeUserId, GuestRequestActor actor, DateTimeOffset now)
    {
        EnsureOpen();
        var takesIt = assigneeUserId == actor.UserId && actor.Area == Area && AssigneeUserId is null;
        if (!actor.CanManage && !takesIt) throw new GuestRequestNotAllowedException("assign");
        if (AssigneeUserId == assigneeUserId) return;

        AssigneeUserId = assigneeUserId;
        Record(GuestRequestHistoryAction.Assigned, $"Assigned to user {assigneeUserId}", actor, now);
        _domainEvents.Add(new GuestRequestAssignedEvent(Id, HotelId, RoomId, assigneeUserId, actor.UserId, now));
    }

    /// <summary>The area starts attending it: Pending → InProgress. Starting it again is a no-op.</summary>
    public void Start(GuestRequestActor actor, DateTimeOffset now)
    {
        EnsureCanWork(actor);
        if (Status == GuestRequestStatus.InProgress) return;
        EnsureOpen();

        AssigneeUserId ??= actor.UserId;
        Status = GuestRequestStatus.InProgress;
        StartedAt = now;
        Record(GuestRequestHistoryAction.Started, null, actor, now);
        _domainEvents.Add(new GuestRequestStatusChangedEvent(Id, HotelId, RoomId, GuestUserId, Status, actor.UserId, now));
    }

    /// <summary>The area resolved it: the guest receives the confirmation (US-11, scenario 4).</summary>
    public void Resolve(string? note, GuestRequestActor actor, DateTimeOffset now)
    {
        EnsureCanWork(actor);
        EnsureOpen();

        AssigneeUserId ??= actor.UserId;
        Status = GuestRequestStatus.Resolved;
        ResolvedAt = now;
        ClosingNote = Normalize(note, NoteMaxLength, "note", GuestRequestErrorCodes.NoteLength);
        Record(GuestRequestHistoryAction.Resolved, ClosingNote, actor, now);
        _domainEvents.Add(new GuestRequestStatusChangedEvent(Id, HotelId, RoomId, GuestUserId, Status, actor.UserId, now));
    }

    /// <summary>The guest withdraws it while nobody started it, or the front desk cancels it (e.g. a duplicate).</summary>
    public void Cancel(string? reason, GuestRequestActor actor, DateTimeOffset now)
    {
        EnsureOpen();
        if (!actor.CanManage && !(BelongsTo(actor) && Status == GuestRequestStatus.Pending))
            throw new GuestRequestNotAllowedException("cancel");

        Status = GuestRequestStatus.Cancelled;
        CancelledAt = now;
        ClosingNote = Normalize(reason, NoteMaxLength, "reason", GuestRequestErrorCodes.NoteLength);
        Record(GuestRequestHistoryAction.Cancelled, ClosingNote, actor, now);
        _domainEvents.Add(new GuestRequestStatusChangedEvent(Id, HotelId, RoomId, GuestUserId, Status, actor.UserId, now));
    }

    /// <summary>The staff of the request's area, the front desk and the administrators work on it.</summary>
    private void EnsureCanWork(GuestRequestActor actor)
    {
        if (actor.IsGuest || !(actor.CanManage || actor.Area == Area))
            throw new GuestRequestNotAllowedException("attend");
    }

    private void EnsureOpen()
    {
        if (!IsOpen) throw new GuestRequestClosedException(Id, Status);
    }

    private void Record(GuestRequestHistoryAction action, string? detail, GuestRequestActor actor, DateTimeOffset now) =>
        _history.Add(new GuestRequestHistoryEntry(action, Status, detail, actor.UserId, actor.Email, now));

    private static string NormalizeTitle(string? title)
    {
        var trimmed = title?.Trim() ?? string.Empty;
        if (trimmed.Length is < TitleMinLength or > TitleMaxLength)
            throw new InvalidFieldException("title", GuestRequestErrorCodes.TitleLength,
                $"Say what you need in {TitleMinLength} to {TitleMaxLength} characters.",
                new Dictionary<string, object?> { ["minLength"] = TitleMinLength, ["maxLength"] = TitleMaxLength });
        return trimmed;
    }

    private static string? Normalize(string? text, int maxLength, string field, string code)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > maxLength)
            throw new InvalidFieldException(field, code, $"The {field} cannot exceed {maxLength} characters.",
                new Dictionary<string, object?> { ["maxLength"] = maxLength });
        return trimmed;
    }
}
