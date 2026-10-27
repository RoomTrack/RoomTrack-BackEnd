namespace RoomTrack.Housekeeping.Domain.Model.Aggregates;

/// <summary>
///     How a hotel distributes its cleaning tasks. A hotel without settings has the automatic assignment off: new
///     tasks wait unassigned until the front desk assigns them.
/// </summary>
public class HotelHousekeepingSettings
{
    /// <summary>EF Core constructor.</summary>
    protected HotelHousekeepingSettings() { }

    public HotelHousekeepingSettings(int hotelId)
    {
        HotelId = hotelId;
    }

    /// <summary>The hotel (logical reference, also the key).</summary>
    public int HotelId { get; private set; }

    /// <summary>
    ///     New cleaning tasks without an explicit assignee go to the active housekeeping staff member with the fewest
    ///     open tasks (US-05).
    /// </summary>
    public bool AutoAssign { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }
    public int? UpdatedByUserId { get; private set; }

    public void Configure(bool autoAssign, int updatedByUserId, DateTimeOffset now)
    {
        AutoAssign = autoAssign;
        UpdatedAt = now;
        UpdatedByUserId = updatedByUserId;
    }
}
