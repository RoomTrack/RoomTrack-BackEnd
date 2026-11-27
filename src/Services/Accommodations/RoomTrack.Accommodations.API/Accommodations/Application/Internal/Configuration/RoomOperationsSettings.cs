using System.ComponentModel.DataAnnotations;

namespace RoomTrack.Accommodations.Application.Internal.Configuration;

/// <summary>Room operations (section <c>Rooms</c>, env vars <c>Rooms__*</c>). Validated at startup.</summary>
public class RoomOperationsSettings
{
    public const string SectionName = "Rooms";

    /// <summary>Hours a room may stay under maintenance before its hotel administrators are alerted (24).</summary>
    [Range(1, 24 * 30)]
    public int MaintenanceAlertAfterHours { get; set; } = 24;

    public TimeSpan MaintenanceAlertAfter => TimeSpan.FromHours(MaintenanceAlertAfterHours);

    /// <summary>
    ///     Minutes before the check-in time a room not ready for its arriving guest is alerted (US-23, 120). The alert
    ///     job runs hourly, so keep it at least 60.
    /// </summary>
    [Range(60, 24 * 60)]
    public int ArrivalAlertLeadMinutes { get; set; } = 120;

    public TimeSpan ArrivalAlertLead => TimeSpan.FromMinutes(ArrivalAlertLeadMinutes);
}
