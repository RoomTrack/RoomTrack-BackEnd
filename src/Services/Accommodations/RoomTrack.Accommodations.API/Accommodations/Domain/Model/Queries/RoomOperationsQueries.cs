namespace BackendAwRoomTrack.API.Accommodations.Domain.Model.Queries;

/// <summary>Every room of a hotel with its status.</summary>
public record GetRoomMapQuery(int HotelId);

/// <summary>The status history of a room, newest first.</summary>
public record GetRoomStatusHistoryQuery(int RoomId, int Limit = 100);
