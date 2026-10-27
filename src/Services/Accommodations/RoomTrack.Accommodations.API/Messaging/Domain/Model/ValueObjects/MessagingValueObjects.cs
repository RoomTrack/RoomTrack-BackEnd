using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.IAM.Domain.Model.Constants;

namespace RoomTrack.Messaging.Domain.Model.ValueObjects;

/// <summary>What a conversation is for.</summary>
public enum ConversationKind
{
    /// <summary>A guest with the front desk of the hotel during their stay (US-20).</summary>
    GuestDesk,
    /// <summary>The channel of an operational area: everyone in the area reads it (US-12, scenario 2).</summary>
    AreaChannel,
    /// <summary>The comments on a task: a cleaning task, an incident or a guest request (US-12, scenarios 1 and 3).</summary>
    TaskThread
}

/// <summary>The tasks a thread can be about.</summary>
public enum ThreadSubject
{
    CleaningTask,
    Incident,
    GuestRequest
}

/// <summary>Who writes or reads a conversation.</summary>
/// <param name="UserId">IAM user id.</param>
/// <param name="DisplayName">Name shown next to their messages.</param>
/// <param name="Role">Their IAM role.</param>
/// <param name="HotelId">The hotel they work at (null for guests and chain administrators).</param>
public sealed record ConversationActor(int UserId, string DisplayName, string Role, int? HotelId)
{
    public bool IsGuest => Role == UserRoles.Guest;

    public bool IsChainAdmin => Role == UserRoles.ChainAdmin;

    public bool IsAdministrator => Role is UserRoles.Admin or UserRoles.ChainAdmin;

    /// <summary>The front desk and the administrators answer the guests.</summary>
    public bool IsFrontDesk => Role == UserRoles.Reception || IsAdministrator;

    public ServiceArea? Area => ServiceAreas.OfRole(Role);

    /// <summary>Staff of <paramref name="hotelId"/> (a chain administrator works at every hotel).</summary>
    public bool WorksAt(int hotelId) => !IsGuest && (IsChainAdmin || HotelId == hotelId);
}
