using RoomTrack.Incidents.Domain.Model.Events;
using RoomTrack.Incidents.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Model.Events;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Incidents.Domain.Model.Aggregates;

/// <summary>
///     A recurring preventive maintenance task of a room (US-30), e.g. "Clean the air conditioner filters" every 90
///     days. On each due date the scheduler turns it into a <see cref="Incident"/> of kind Preventive for maintenance,
///     and a few days before it reminds the maintenance staff.
/// </summary>
/// <remarks>
///     Missed cycles are not piled up: when the scheduler runs late, one task is generated and the next due date is the
///     first one after today.
/// </remarks>
public class PreventiveMaintenancePlan : IHasDomainEvents
{
    public const int TitleMinLength = 3;
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 1000;
    public const int MinFrequencyDays = 1;
    public const int MaxFrequencyDays = 365;
    public const int MaxReminderDaysBefore = 30;

    private readonly List<IEvent> _domainEvents = [];

    /// <summary>EF Core constructor.</summary>
    protected PreventiveMaintenancePlan()
    {
        Title = string.Empty;
    }

    private PreventiveMaintenancePlan(int hotelId, int roomId, string title, string? description, int frequencyDays,
        DateOnly firstDueOn, int reminderDaysBefore, int createdByUserId, DateTimeOffset now)
    {
        HotelId = hotelId;
        RoomId = roomId;
        Title = title;
        Description = description;
        FrequencyDays = frequencyDays;
        NextDueOn = firstDueOn;
        ReminderDaysBefore = reminderDaysBefore;
        IsActive = true;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
    }

    /// <summary>Maintenance (or an administrator) defines a preventive task of a room and how often it repeats.</summary>
    /// <param name="hotelId">Hotel of the room.</param>
    /// <param name="roomId">The room.</param>
    /// <param name="title">What to do (3-120 characters).</param>
    /// <param name="description">Details, optional.</param>
    /// <param name="frequencyDays">Days between occurrences (1-365).</param>
    /// <param name="firstDueOn">When the first task is generated.</param>
    /// <param name="reminderDaysBefore">Days before each due date the staff is reminded (0: none).</param>
    /// <param name="createdByUserId">Who schedules it.</param>
    /// <param name="today">The hotel's current date: the first due date cannot be in the past.</param>
    /// <param name="now">The current instant.</param>
    public static PreventiveMaintenancePlan Schedule(int hotelId, int roomId, string? title, string? description,
        int frequencyDays, DateOnly firstDueOn, int reminderDaysBefore, int createdByUserId, DateOnly today, DateTimeOffset now)
    {
        ValidateSchedule(frequencyDays, reminderDaysBefore);
        if (firstDueOn < today)
            throw new InvalidFieldException("firstDueOn", PreventiveMaintenanceErrorCodes.FirstDueInPast,
                "The first due date cannot be in the past.");
        return new PreventiveMaintenancePlan(hotelId, roomId, NormalizeTitle(title), NormalizeDescription(description),
            frequencyDays, firstDueOn, reminderDaysBefore, createdByUserId, now);
    }

    public int Id { get; private set; }
    public int HotelId { get; private set; }
    public int RoomId { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }

    /// <summary>Days between two occurrences.</summary>
    public int FrequencyDays { get; private set; }

    /// <summary>When the next task is generated.</summary>
    public DateOnly NextDueOn { get; private set; }

    /// <summary>Days before the due date the maintenance staff is reminded (0: no reminder).</summary>
    public int ReminderDaysBefore { get; private set; }

    /// <summary>The due date whose reminder was already sent (one reminder per cycle).</summary>
    public DateOnly? ReminderSentForDueOn { get; private set; }

    public bool IsActive { get; private set; }
    public int CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the last task was generated, and which incident it is.</summary>
    public DateTimeOffset? LastGeneratedAt { get; private set; }
    public int? LastIncidentId { get; private set; }

    /// <summary>How many tasks it generated.</summary>
    public int GeneratedCount { get; private set; }

    public DateTimeOffset? DeactivatedAt { get; private set; }

    public IReadOnlyCollection<IEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Its task must be generated today (the due date arrived or passed).</summary>
    public bool IsDue(DateOnly today) => IsActive && NextDueOn <= today;

    /// <summary>The due date is within the reminder window and its reminder was not sent yet.</summary>
    public bool IsReminderDue(DateOnly today) =>
        IsActive && ReminderDaysBefore > 0 && today < NextDueOn && today >= NextDueOn.AddDays(-ReminderDaysBefore)
        && ReminderSentForDueOn != NextDueOn;

    /// <summary>Reminds the maintenance staff of the coming task (once per cycle). Returns false when not due.</summary>
    public bool Remind(DateOnly today, DateTimeOffset now)
    {
        if (!IsReminderDue(today)) return false;
        ReminderSentForDueOn = NextDueOn;
        _domainEvents.Add(new PreventiveMaintenanceReminderDueEvent(Id, HotelId, RoomId, Title, NextDueOn, now));
        return true;
    }

    /// <summary>
    ///     The task of this cycle was generated as incident <paramref name="incidentId"/>: the next due date is the first
    ///     one after <paramref name="today"/>.
    /// </summary>
    public void RecordGenerated(int incidentId, DateOnly today, DateTimeOffset now)
    {
        if (!IsDue(today))
            throw new BusinessRuleViolationException(PreventiveMaintenanceErrorCodes.NotDue,
                $"Preventive maintenance plan {Id} is not due (next due on {NextDueOn:yyyy-MM-dd}).");
        LastIncidentId = incidentId;
        LastGeneratedAt = now;
        GeneratedCount++;
        while (NextDueOn <= today) NextDueOn = NextDueOn.AddDays(FrequencyDays);
    }

    /// <summary>Changes how often it repeats and when the next task is due.</summary>
    public void Reschedule(int frequencyDays, DateOnly nextDueOn, int reminderDaysBefore, DateOnly today)
    {
        EnsureActive();
        ValidateSchedule(frequencyDays, reminderDaysBefore);
        if (nextDueOn < today)
            throw new InvalidFieldException("nextDueOn", PreventiveMaintenanceErrorCodes.FirstDueInPast,
                "The next due date cannot be in the past.");
        FrequencyDays = frequencyDays;
        ReminderDaysBefore = reminderDaysBefore;
        if (NextDueOn != nextDueOn) ReminderSentForDueOn = null;
        NextDueOn = nextDueOn;
    }

    /// <summary>Stops generating tasks (the tasks already generated stay). Deactivating twice is a no-op.</summary>
    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false;
        DeactivatedAt = now;
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw new BusinessRuleViolationException(PreventiveMaintenanceErrorCodes.Inactive,
                $"Preventive maintenance plan {Id} is no longer active.");
    }

    private static void ValidateSchedule(int frequencyDays, int reminderDaysBefore)
    {
        if (frequencyDays is < MinFrequencyDays or > MaxFrequencyDays)
            throw new InvalidFieldException("frequencyDays", PreventiveMaintenanceErrorCodes.FrequencyRange,
                $"The frequency must be between {MinFrequencyDays} and {MaxFrequencyDays} days.",
                new Dictionary<string, object?> { ["min"] = MinFrequencyDays, ["max"] = MaxFrequencyDays });
        if (reminderDaysBefore < 0 || reminderDaysBefore > MaxReminderDaysBefore || reminderDaysBefore >= frequencyDays)
            throw new InvalidFieldException("reminderDaysBefore", PreventiveMaintenanceErrorCodes.ReminderRange,
                $"The reminder must be between 0 and {MaxReminderDaysBefore} days before, and shorter than the frequency.",
                new Dictionary<string, object?> { ["max"] = MaxReminderDaysBefore });
    }

    private static string NormalizeTitle(string? title)
    {
        var trimmed = title?.Trim() ?? string.Empty;
        if (trimmed.Length is < TitleMinLength or > TitleMaxLength)
            throw new InvalidFieldException("title", PreventiveMaintenanceErrorCodes.TitleLength,
                $"The title must have between {TitleMinLength} and {TitleMaxLength} characters.",
                new Dictionary<string, object?> { ["minLength"] = TitleMinLength, ["maxLength"] = TitleMaxLength });
        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > DescriptionMaxLength)
            throw new InvalidFieldException("description", PreventiveMaintenanceErrorCodes.DescriptionLength,
                $"The description cannot exceed {DescriptionMaxLength} characters.",
                new Dictionary<string, object?> { ["maxLength"] = DescriptionMaxLength });
        return trimmed;
    }
}
