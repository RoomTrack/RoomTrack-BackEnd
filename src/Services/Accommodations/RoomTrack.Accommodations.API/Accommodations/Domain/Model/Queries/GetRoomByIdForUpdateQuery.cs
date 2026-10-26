namespace RoomTrack.Accommodations.Domain.Model.Queries;

/// <summary>
///     Retrieves a room and locks it until the current transaction ends, so the work that depends on the room (e.g.
///     "a room has at most one open cleaning task") runs one request after the other. Must be sent inside a
///     transaction.
/// </summary>
/// <param name="RoomId">The room to lock.</param>
public record GetRoomByIdForUpdateQuery(int RoomId);
