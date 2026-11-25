using RoomTrack.Shared.Domain.Model.Events;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.ShiftHandovers.Domain.Model.Events;
using RoomTrack.ShiftHandovers.Domain.Model.Exceptions;
using RoomTrack.ShiftHandovers.Domain.Model.ValueObjects;

namespace RoomTrack.ShiftHandovers.Domain.Model.Aggregates;

/// <summary>
///     The summary a staff member leaves when their shift ends (US-32): their notes and a snapshot of the tasks and
///     incidents still open, so the next shift of the area continues without losing information. Whoever reads it
///     confirms it, and the handover records who and when.
/// </summary>
/// <remarks>
///     The items are a snapshot (title, status and priority at the end of the shift); the cleaning tasks and incidents
///     themselves keep living in their contexts and are referenced by id.
/// </remarks>
public class ShiftHandover : IHasDomainEvents
{
    public const int NotesMaxLength = 2000;
    public const int MaxItems = 200;
    public const int MaxEmailLength = 254;

    private readonly List<IEvent> _domainEvents = [];
    private readonly List<ShiftHandoverItem> _items = [];
    private readonly List<ShiftHandoverAcknowledgement> _acknowledgements = [];
    private bool _pendingCreated;

    /// <summary>EF Core constructor.</summary>
    protected ShiftHandover() { }

    private ShiftHandover(int hotelId, ShiftArea area, ShiftHandoverActor author, string? notes,
        IReadOnlyList<ShiftHandoverItemSnapshot> items, DateTimeOffset now)
    {
        HotelId = hotelId;
        Area = area;
        AuthorUserId = author.UserId;
        AuthorEmail = Truncate(author.Email);
        Notes = notes;
        CreatedAt = now;
        _items.AddRange(items.Select(item => new ShiftHandoverItem(item)));
        _pendingCreated = true;
    }

    /// <summary>
    ///     A staff member ends their shift with <paramref name="notes"/> and the <paramref name="openItems"/> they leave
    ///     open. A handover with neither notes nor open items says nothing and is refused.
    /// </summary>
    public static ShiftHandover Create(int hotelId, ShiftArea area, ShiftHandoverActor author, string? notes,
        IReadOnlyList<ShiftHandoverItemSnapshot> openItems, DateTimeOffset now)
    {
        var trimmed = notes?.Trim();
        if (string.IsNullOrEmpty(trimmed)) trimmed = null;
        if (trimmed is { Length: > NotesMaxLength })
            throw new InvalidFieldException("notes", ShiftHandoverErrorCodes.NotesLength,
                $"The notes cannot exceed {NotesMaxLength} characters.",
                new Dictionary<string, object?> { ["maxLength"] = NotesMaxLength });
        if (trimmed is null && openItems.Count == 0)
            throw new BusinessRuleViolationException(ShiftHandoverErrorCodes.Empty,
                "There is nothing to hand over: write a note or leave open tasks or incidents.");
        return new ShiftHandover(hotelId, area, author, trimmed, openItems.Take(MaxItems).ToList(), now);
    }

    public int Id { get; private set; }
    public int HotelId { get; private set; }

    /// <summary>The area whose next shift reads it.</summary>
    public ShiftArea Area { get; private set; }

    public int AuthorUserId { get; private set; }
    public string? AuthorEmail { get; private set; }

    /// <summary>What the next shift must know, in the author's words.</summary>
    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Tasks and incidents open when the shift ended.</summary>
    public IReadOnlyList<ShiftHandoverItem> Items => _items.AsReadOnly();

    /// <summary>Who confirmed reading it, and when.</summary>
    public IReadOnlyList<ShiftHandoverAcknowledgement> Acknowledgements => _acknowledgements.AsReadOnly();

    public IReadOnlyCollection<IEvent> DomainEvents
    {
        get
        {
            if (_pendingCreated && Id > 0)
            {
                _domainEvents.Insert(0, new ShiftHandoverCreatedEvent(Id, HotelId, Area, AuthorUserId, _items.Count, CreatedAt));
                _pendingCreated = false;
            }
            return _domainEvents.AsReadOnly();
        }
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    ///     A colleague confirms they read the handover: who and when are recorded. The author does not confirm their own
    ///     handover; confirming twice keeps the first confirmation.
    /// </summary>
    public ShiftHandoverAcknowledgement Acknowledge(ShiftHandoverActor reader, DateTimeOffset now)
    {
        if (reader.UserId == AuthorUserId)
            throw new BusinessRuleViolationException(ShiftHandoverErrorCodes.OwnHandover,
                "You cannot confirm your own shift handover: the next shift confirms it.");
        if (_acknowledgements.FirstOrDefault(ack => ack.UserId == reader.UserId) is { } existing) return existing;

        var acknowledgement = new ShiftHandoverAcknowledgement(reader.UserId, Truncate(reader.Email), now);
        _acknowledgements.Add(acknowledgement);
        _domainEvents.Add(new ShiftHandoverAcknowledgedEvent(Id, HotelId, AuthorUserId, reader.UserId, now));
        return acknowledgement;
    }

    /// <summary>The reader already confirmed it.</summary>
    public bool IsAcknowledgedBy(int userId) => _acknowledgements.Any(ack => ack.UserId == userId);

    private static string? Truncate(string? email) => email is { Length: > MaxEmailLength } ? email[..MaxEmailLength] : email;
}

/// <summary>One task or incident open when the shift ended (snapshot).</summary>
public class ShiftHandoverItem
{
    public const int SummaryMaxLength = 200;

    /// <summary>EF Core constructor.</summary>
    protected ShiftHandoverItem()
    {
        Summary = string.Empty;
        Status = string.Empty;
        Priority = string.Empty;
    }

    internal ShiftHandoverItem(ShiftHandoverItemSnapshot snapshot)
    {
        Kind = snapshot.Kind;
        ReferenceId = snapshot.ReferenceId;
        RoomId = snapshot.RoomId;
        Summary = snapshot.Summary.Length > SummaryMaxLength ? snapshot.Summary[..SummaryMaxLength] : snapshot.Summary;
        Status = snapshot.Status;
        Priority = snapshot.Priority;
        AssigneeUserId = snapshot.AssigneeUserId;
    }

    public long Id { get; private set; }
    public int ShiftHandoverId { get; private set; }
    public ShiftHandoverItemKind Kind { get; private set; }

    /// <summary>The cleaning task or incident id.</summary>
    public int ReferenceId { get; private set; }

    public int RoomId { get; private set; }
    public string Summary { get; private set; }
    public string Status { get; private set; }
    public string Priority { get; private set; }
    public int? AssigneeUserId { get; private set; }
}

/// <summary>Who confirmed reading a handover, and when (US-32).</summary>
public class ShiftHandoverAcknowledgement
{
    /// <summary>EF Core constructor.</summary>
    protected ShiftHandoverAcknowledgement() { }

    internal ShiftHandoverAcknowledgement(int userId, string? email, DateTimeOffset acknowledgedAt)
    {
        UserId = userId;
        Email = email;
        AcknowledgedAt = acknowledgedAt;
    }

    public long Id { get; private set; }
    public int ShiftHandoverId { get; private set; }
    public int UserId { get; private set; }
    public string? Email { get; private set; }
    public DateTimeOffset AcknowledgedAt { get; private set; }
}
