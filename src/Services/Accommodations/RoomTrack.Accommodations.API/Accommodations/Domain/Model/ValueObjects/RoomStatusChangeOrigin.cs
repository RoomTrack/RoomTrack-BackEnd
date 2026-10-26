namespace RoomTrack.Accommodations.Domain.Model.ValueObjects;

/// <summary>What changed the status of a room.</summary>
public enum RoomStatusChangeOrigin
{
    /// <summary>A staff member changed it.</summary>
    Staff,
    /// <summary>A guest completed the digital check-in: the room became Occupied.</summary>
    CheckIn,
    /// <summary>The guest checked out: the room went from Occupied to Cleaning (pending cleaning).</summary>
    CheckOut,
    /// <summary>A blocking incident took the room out of service, or its resolution released it to cleaning.</summary>
    Incident,
    /// <summary>Housekeeping started (Cleaning) or finished (Available) the cleaning task of the room.</summary>
    Housekeeping
}
