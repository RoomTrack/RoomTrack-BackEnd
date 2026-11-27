using System.Net.Mail;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.StaffPerformance.Domain.Model.Exceptions;

namespace RoomTrack.StaffPerformance.Domain.Model.Aggregates;

/// <summary>How often a scheduled report is sent.</summary>
public enum ReportFrequency
{
    /// <summary>Every Monday, with the previous 7 days.</summary>
    Weekly,
    /// <summary>The 1st of every month, with the previous month.</summary>
    Monthly
}

/// <summary>What the administrator configures in a report schedule.</summary>
public sealed record ReportScheduleSettings(
    ReportFrequency Frequency,
    IReadOnlyCollection<string> Recipients,
    bool IncludeProductivity = true,
    bool IncludeServiceTimes = true,
    bool IsActive = true);

/// <summary>
///     The automatic delivery of the staff reports of a hotel by e-mail (US-34): the productivity (US-24) and the
///     service times (US-25) of the period that just ended, sent to the configured recipients every week or month.
/// </summary>
/// <remarks>
///     The external scheduler sends the due reports every hour; a schedule is due from <see cref="NextRunAt"/>. A
///     weekly report goes on Mondays and a monthly one on the 1st, at <see cref="SendHourUtc"/>:00 UTC (08:00 in Lima),
///     covering the period that ends then. When the scheduler was down for a while, the missed periods are skipped:
///     only the last one is sent.
/// </remarks>
public class ReportSchedule
{
    public const int MaxRecipients = 10;
    public const int MaxEmailLength = 254;
    public const int SendHourUtc = 13;
    private const char RecipientSeparator = ',';

    /// <summary>EF Core constructor.</summary>
    protected ReportSchedule()
    {
        RecipientList = string.Empty;
    }

    private ReportSchedule(int hotelId, int createdByUserId, DateTimeOffset now)
    {
        HotelId = hotelId;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        RecipientList = string.Empty;
    }

    /// <summary>An administrator schedules the reports of their hotel.</summary>
    public static ReportSchedule Create(int hotelId, ReportScheduleSettings settings, int createdByUserId, DateTimeOffset now)
    {
        var schedule = new ReportSchedule(hotelId, createdByUserId, now);
        schedule.Apply(settings, now);
        return schedule;
    }

    public int Id { get; private set; }
    public int HotelId { get; private set; }
    public ReportFrequency Frequency { get; private set; }

    /// <summary>The e-mails of the recipients, as stored (comma separated).</summary>
    public string RecipientList { get; private set; }

    public IReadOnlyList<string> Recipients => RecipientList.Split(RecipientSeparator, StringSplitOptions.RemoveEmptyEntries);

    public bool IncludeProductivity { get; private set; }
    public bool IncludeServiceTimes { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>When the next report goes out; it covers the period that ends at this instant.</summary>
    public DateTimeOffset NextRunAt { get; private set; }

    public DateTimeOffset? LastSentAt { get; private set; }
    public int CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The administrator changes the schedule. A new frequency starts at its next boundary.</summary>
    public void Update(ReportScheduleSettings settings, DateTimeOffset now) => Apply(settings, now);

    public bool IsDue(DateTimeOffset now) => IsActive && now >= NextRunAt;

    /// <summary>The period the due report covers: the week or month that ends at <see cref="NextRunAt"/>.</summary>
    public (DateTimeOffset From, DateTimeOffset To) DuePeriod => (Previous(NextRunAt), NextRunAt);

    /// <summary>The last 7 days or the last month until <paramref name="now"/> (to send a report right away).</summary>
    public (DateTimeOffset From, DateTimeOffset To) PeriodUntil(DateTimeOffset now) => (Previous(now), now);

    /// <summary>The due report was sent: the next one is the first boundary after <paramref name="now"/>.</summary>
    public void MarkSent(DateTimeOffset now)
    {
        LastSentAt = now;
        NextRunAt = NextBoundary(Frequency, now);
    }

    /// <summary>The first send instant of <paramref name="frequency"/> strictly after <paramref name="after"/>.</summary>
    public static DateTimeOffset NextBoundary(ReportFrequency frequency, DateTimeOffset after)
    {
        var utc = after.ToUniversalTime();
        var day = new DateTimeOffset(utc.Year, utc.Month, utc.Day, SendHourUtc, 0, 0, TimeSpan.Zero);
        if (frequency == ReportFrequency.Weekly)
        {
            var candidate = day.AddDays(((int)DayOfWeek.Monday - (int)day.DayOfWeek + 7) % 7);
            return candidate > utc ? candidate : candidate.AddDays(7);
        }

        var first = new DateTimeOffset(utc.Year, utc.Month, 1, SendHourUtc, 0, 0, TimeSpan.Zero);
        return first > utc ? first : first.AddMonths(1);
    }

    private DateTimeOffset Previous(DateTimeOffset end) =>
        Frequency == ReportFrequency.Weekly ? end.AddDays(-7) : end.AddMonths(-1);

    private void Apply(ReportScheduleSettings settings, DateTimeOffset now)
    {
        if (!settings.IncludeProductivity && !settings.IncludeServiceTimes)
            throw new InvalidFieldException("includeProductivity", StaffPerformanceErrorCodes.NoReportSelected,
                "Choose at least one report: productivity or service times.");

        var recipients = NormalizeRecipients(settings.Recipients);
        var frequencyChanged = Id == 0 || settings.Frequency != Frequency;
        Frequency = settings.Frequency;
        RecipientList = string.Join(RecipientSeparator, recipients);
        IncludeProductivity = settings.IncludeProductivity;
        IncludeServiceTimes = settings.IncludeServiceTimes;
        var reactivated = settings.IsActive && !IsActive;
        IsActive = settings.IsActive;
        // A new or reactivated schedule never sends the periods it missed.
        if (frequencyChanged || reactivated) NextRunAt = NextBoundary(Frequency, now);
    }

    private static List<string> NormalizeRecipients(IReadOnlyCollection<string>? recipients)
    {
        var emails = (recipients ?? [])
            .Select(email => email?.Trim().ToLowerInvariant() ?? string.Empty)
            .Where(email => email.Length > 0)
            .Distinct()
            .ToList();
        if (emails.Count == 0)
            throw new InvalidFieldException("recipients", StaffPerformanceErrorCodes.RecipientsRequired,
                "Send at least one e-mail to deliver the reports to.");
        if (emails.Count > MaxRecipients)
            throw new InvalidFieldException("recipients", StaffPerformanceErrorCodes.TooManyRecipients,
                $"A schedule has at most {MaxRecipients} recipients.",
                new Dictionary<string, object?> { ["max"] = MaxRecipients });
        foreach (var email in emails)
            if (email.Length > MaxEmailLength || email.Contains(RecipientSeparator)
                || !MailAddress.TryCreate(email, out var address) || address.Address != email)
                throw new InvalidFieldException("recipients", StaffPerformanceErrorCodes.RecipientInvalid,
                    $"'{email}' is not a valid e-mail.");
        return emails;
    }
}
