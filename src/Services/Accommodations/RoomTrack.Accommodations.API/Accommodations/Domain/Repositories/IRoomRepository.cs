using BackendAwRoomTrack.API.Accommodations.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Shared.Domain.Repositories;

namespace BackendAwRoomTrack.API.Accommodations.Domain.Repositories;

/// <summary>
/// Repository interface for managing Room aggregates.
/// </summary>
public interface IRoomRepository : IBaseRepository<Room>
{
    /// <summary>
    ///     Loads the room and locks its row until the current transaction ends (<c>SELECT ... FOR UPDATE</c>), so
    ///     concurrent bookings of the same room are serialized (R1). Must run inside a transaction.
    /// </summary>
    Task<Room?> FindByIdForUpdateAsync(int id);

    /// <summary>Rooms of <paramref name="hotelId"/> (or of every hotel) that are not under maintenance.</summary>
    Task<IEnumerable<Room>> FindOfferedForBookingAsync(int? hotelId);

    /// <summary>Every room of a hotel with its type, by id (room map).</summary>
    Task<IReadOnlyList<Room>> ListByHotelAsync(int hotelId);

    /// <summary>True when another room of the hotel already uses <paramref name="number"/>.</summary>
    Task<bool> ExistsNumberInHotelAsync(int hotelId, string number, int? excludingRoomId = null);

    /// <summary>Number of each of <paramref name="roomIds"/>, in one query.</summary>
    Task<IReadOnlyDictionary<int, string>> FindNumbersAsync(IReadOnlyCollection<int> roomIds);

    /// <summary>Rooms currently under maintenance.</summary>
    Task<IReadOnlyList<Room>> ListInMaintenanceAsync();
}
