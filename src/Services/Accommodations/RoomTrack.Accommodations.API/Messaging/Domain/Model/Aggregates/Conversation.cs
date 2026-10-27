using System.Globalization;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.Messaging.Domain.Model.Entities;
using RoomTrack.Messaging.Domain.Model.Events;
using RoomTrack.Messaging.Domain.Model.Exceptions;
using RoomTrack.Messaging.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Events;

namespace RoomTrack.Messaging.Domain.Model.Aggregates;

/// <summary>
///     A conversation of a hotel, inside the platform instead of scattered external channels (US-12, US-20): a guest
///     with the front desk, the channel of an area, or the comments on a task. There is one conversation per guest, area
///     or task of each hotel (<see cref="Key"/>), created with its first message.
/// </summary>
/// <remarks>
///     The messages are entities of their own (<see cref="ConversationMessage"/>), saved next to the conversation and
///     read page by page: a conversation never loads its whole history. Who may read and write is decided here
///     (<see cref="CanAccess"/>).
/// </remarks>
public class Conversation : IHasDomainEvents
{
    public const int MaxKeyLength = 60;

    private readonly List<IEvent> _domainEvents = [];

    /// <summary>EF Core constructor.</summary>
    protected Conversation()
    {
        Key = string.Empty;
    }

    private Conversation(int hotelId, ConversationKind kind, string key, DateTimeOffset now)
    {
        HotelId = hotelId;
        Kind = kind;
        Key = key;
        CreatedAt = now;
    }

    public static Conversation WithFrontDesk(int hotelId, int guestUserId, DateTimeOffset now) =>
        new(hotelId, ConversationKind.GuestDesk, KeyOfGuest(guestUserId), now) { GuestUserId = guestUserId };

    public static Conversation OfArea(int hotelId, ServiceArea area, DateTimeOffset now) =>
        new(hotelId, ConversationKind.AreaChannel, KeyOfArea(area), now) { Area = area };

    public static Conversation AboutTask(int hotelId, ThreadSubject subject, int subjectId, DateTimeOffset now) =>
        new(hotelId, ConversationKind.TaskThread, KeyOfTask(subject, subjectId), now) { Subject = subject, SubjectId = subjectId };

    public static string KeyOfGuest(int guestUserId) => $"guest:{guestUserId.ToString(CultureInfo.InvariantCulture)}";
    public static string KeyOfArea(ServiceArea area) => $"area:{area}";
    public static string KeyOfTask(ThreadSubject subject, int subjectId) =>
        $"{subject}:{subjectId.ToString(CultureInfo.InvariantCulture)}";

    public int Id { get; private set; }
    public int HotelId { get; private set; }
    public ConversationKind Kind { get; private set; }

    /// <summary>Unique per hotel and kind: the guest, the area or the task of the conversation.</summary>
    public string Key { get; private set; }

    public int? GuestUserId { get; private set; }
    public ServiceArea? Area { get; private set; }
    public ThreadSubject? Subject { get; private set; }
    public int? SubjectId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastMessageAt { get; private set; }
    public int MessageCount { get; private set; }

    public IReadOnlyCollection<IEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    ///     Who reads and writes it: the guest and the front desk of the hotel (guest desk); the staff of the area and
    ///     the administrators (area channel); any staff member of the hotel (task thread).
    /// </summary>
    public bool CanAccess(ConversationActor actor) => Kind switch
    {
        ConversationKind.GuestDesk => actor.IsGuest ? actor.UserId == GuestUserId : actor.WorksAt(HotelId) && actor.IsFrontDesk,
        ConversationKind.AreaChannel => actor.WorksAt(HotelId) && (actor.IsAdministrator || actor.Area == Area),
        _ => actor.WorksAt(HotelId)
    };

    /// <summary>Writes a message; the other side is notified (US-12, US-20).</summary>
    public ConversationMessage Post(ConversationActor sender, string body, DateTimeOffset now)
    {
        if (!CanAccess(sender)) throw new ConversationAccessDeniedException();
        var message = new ConversationMessage(this, sender, body, now);
        LastMessageAt = now;
        MessageCount++;
        _domainEvents.Add(new MessagePostedEvent(this, message.Preview, sender.UserId, sender.IsGuest, sender.DisplayName, now));
        return message;
    }
}
