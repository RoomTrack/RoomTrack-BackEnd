using System.ComponentModel.DataAnnotations;

namespace BackendAwRoomTrack.API.Incidents.Application.Internal.Configuration;

/// <summary>Incident follow-up (section <c>Incidents</c>, env vars <c>Incidents__*</c>). Validated at startup.</summary>
public class IncidentSettings
{
    public const string SectionName = "Incidents";

    /// <summary>
    ///     Minutes a critical incident may stay without being started before the hotel administrators are alerted (60).
    /// </summary>
    [Range(5, 24 * 60)]
    public int CriticalEscalationAfterMinutes { get; set; } = 60;

    public TimeSpan CriticalEscalationAfter => TimeSpan.FromMinutes(CriticalEscalationAfterMinutes);
}
