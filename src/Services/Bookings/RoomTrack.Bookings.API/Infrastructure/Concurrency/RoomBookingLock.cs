using BackendAwRoomTrack.API.Accommodations.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;
using RoomTrack.BuildingBlocks.Clients;

namespace RoomTrack.Bookings.API.Infrastructure.Concurrency;

/// <summary>
///     Lock row of a room in the Bookings database (table <c>room_booking_locks</c>). In the monolith the booking
///     transaction locked the room row itself; now the rooms live in another service's database, so the Bookings
///     service serializes the bookings of a room on a row of its own.
/// </summary>
public class RoomBookingLock
{
    public int RoomId { get; set; }
}

/// <summary>
///     The Accommodations port as the Bookings service sees it: the Accommodations service's internal API, plus the
///     local lock of R1 (no two bookings of a room are created or moved at the same time).
/// </summary>
public class BookingsAccommodationsFacade(HttpClient httpClient, AppDbContext context)
    : HttpAccommodationsContextFacade(httpClient)
{
    /// <summary>
    ///     Locks the room's row of <c>room_booking_locks</c> (<c>SELECT ... FOR UPDATE</c>) until the current
    ///     transaction ends, then fetches the room. Concurrent bookings of the same room wait here, so the
    ///     availability check and the insert run one after the other. Must be called inside a transaction.
    /// </summary>
    public override async Task<RoomOffer?> LockRoomForBookingAsync(int roomId)
    {
        if (roomId <= 0) return null;
        if (context.Database.IsRelational())
        {
            if (context.Database.CurrentTransaction is null)
                throw new InvalidOperationException("The room lock must be taken inside the booking transaction.");

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT IGNORE INTO room_booking_locks (room_id) VALUES ({roomId})");
            await context.Database
                .SqlQuery<int>($"SELECT room_id AS Value FROM room_booking_locks WHERE room_id = {roomId} FOR UPDATE")
                .ToListAsync();
        }

        return await FetchRoomAsync(roomId);
    }
}
