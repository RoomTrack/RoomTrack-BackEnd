using BackendAwRoomTrack.API.Accommodations.Domain.Model.Entities;

namespace BackendAwRoomTrack.API.Accommodations.Domain.Repositories;

/// <summary>Status history of the rooms. Append-only.</summary>
public interface IRoomStatusChangeRepository
{
    Task AddAsync(RoomStatusChange change);

    /// <summary>The changes of a room, newest first.</summary>
    Task<IReadOnlyList<RoomStatusChange>> ListByRoomAsync(int roomId, int limit);
}
