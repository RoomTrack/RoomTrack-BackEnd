namespace BackendAwRoomTrack.API.Bookings.Interfaces.ACL;

/// <summary>
///     Read-only view of the reservations of rooms, for the Accommodations context (e.g. a room with active bookings
///     cannot be deleted; a guest controls the devices of the room of their current stay). Separate from the Bookings
///     facade used by Payments so it depends on nothing of Accommodations (no dependency cycle between the two
///     contexts).
/// </summary>
public interface IRoomReservationsFacade
{
    /// <summary>How many active bookings (Pending, Confirmed, CheckedIn) hold each of <paramref name="roomIds"/>.</summary>
    Task<IReadOnlyDictionary<int, int>> CountActiveBookingsAsync(IReadOnlyCollection<int> roomIds);

    /// <summary>
    ///     True when the guest has a Confirmed booking of <paramref name="roomId"/> whose dates include
    ///     <paramref name="day"/> (R5: a guest controls the devices of the room they are staying in).
    /// </summary>
    Task<bool> HasCurrentConfirmedStayAsync(int guestUserId, int roomId, DateTime day);
}
