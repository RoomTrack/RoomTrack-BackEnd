namespace BackendAwRoomTrack.API.Accommodations.Domain.Model.ValueObjects;

/// <summary>What changed the status of a room.</summary>
public enum RoomStatusChangeOrigin
{
    /// <summary>A staff member changed it.</summary>
    Staff,
    /// <summary>A guest completed the digital check-in: the room became Occupied.</summary>
    CheckIn
}
