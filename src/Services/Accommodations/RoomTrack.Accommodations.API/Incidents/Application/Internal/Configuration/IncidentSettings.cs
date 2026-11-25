using System.ComponentModel.DataAnnotations;

namespace RoomTrack.Incidents.Application.Internal.Configuration;

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

    /// <summary>US-10: more than this many similar incidents of a room within the window form a pattern (3).</summary>
    [Range(1, 50)]
    public int RecurrenceThreshold { get; set; } = 3;

    /// <summary>US-10: days looked back for similar incidents of the room (30, "in a month").</summary>
    [Range(1, 365)]
    public int RecurrenceWindowDays { get; set; } = 30;

    /// <summary>Time zone of the hotels, for the due dates of the preventive maintenance plans (US-30).</summary>
    [Required]
    public string TimeZoneId { get; set; } = "America/Lima";

    /// <summary>The current date of the hotels.</summary>
    public DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId)).DateTime);
}
