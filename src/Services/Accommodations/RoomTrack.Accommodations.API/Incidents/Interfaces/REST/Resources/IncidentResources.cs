using System.ComponentModel.DataAnnotations;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Shared.Infrastructure.Interfaces.ASP.Validation;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Incidents.Interfaces.REST.Resources;

/// <summary>A new incident of a room.</summary>
public record ReportIncidentResource : IValidatableObject
{
    /// <summary>The damaged room.</summary>
    /// <example>12</example>
    [Required]
    public int? RoomId { get; init; }

    /// <summary>Short summary (3-120 characters).</summary>
    /// <example>Leaking shower</example>
    [Required]
    public string? Title { get; init; }

    /// <summary>Details, optional (up to 1000 characters).</summary>
    /// <example>Water drips from the shower head even when it is closed.</example>
    public string? Description { get; init; }

    /// <summary>Low, Medium, High or Critical (case-insensitive).</summary>
    /// <example>High</example>
    [Required]
    public string? Priority { get; init; }

    /// <summary>The room cannot be used until the incident is resolved: it goes to Maintenance meanwhile.</summary>
    /// <example>false</example>
    public bool BlocksRoom { get; init; }

    /// <summary>The validated priority as a domain value (a method, so model validation never evaluates it).</summary>
    public IncidentPriority ToPriority() => Enum.Parse<IncidentPriority>(Priority!, ignoreCase: true);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        IncidentPriorityValidation.Validate(Priority);
}

/// <summary>Who takes care of an incident.</summary>
public record AssignIncidentResource
{
    /// <summary>A maintenance staff member of the incident's hotel (IAM user id).</summary>
    /// <example>7</example>
    [Required]
    public int? AssigneeUserId { get; init; }
}

/// <summary>How an incident was resolved.</summary>
public record ResolveIncidentResource
{
    /// <summary>What was done (required, up to 1000 characters).</summary>
    /// <example>Replaced the shower cartridge; no more leaks.</example>
    public string? ResolutionNotes { get; init; }
}

/// <summary>New urgency of an incident.</summary>
public record ChangeIncidentPriorityResource : IValidatableObject
{
    /// <summary>Low, Medium, High or Critical (case-insensitive).</summary>
    /// <example>Critical</example>
    [Required]
    public string? Priority { get; init; }

    public IncidentPriority ToPriority() => Enum.Parse<IncidentPriority>(Priority!, ignoreCase: true);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        IncidentPriorityValidation.Validate(Priority);
}

internal static class IncidentPriorityValidation
{
    public static IEnumerable<ValidationResult> Validate(string? priority)
    {
        if (!string.IsNullOrWhiteSpace(priority)
            && (!Enum.TryParse<IncidentPriority>(priority, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed) || int.TryParse(priority, out _)))
            yield return new CodedValidationResult(ErrorCodes.FieldNotAllowed,
                $"Priority must be one of: {string.Join(", ", Enum.GetNames<IncidentPriority>())}.", ["priority"],
                new Dictionary<string, object?> { ["allowed"] = Enum.GetNames<IncidentPriority>() });
    }
}

/// <summary>One line of the history of an incident.</summary>
/// <param name="Action">Reported, Assigned, Started, PriorityChanged, Escalated or Resolved.</param>
/// <param name="Status">Status of the incident after it.</param>
/// <param name="Detail">What changed (new priority, assignee, resolution notes).</param>
/// <param name="ActorUserId">Who (null for the system).</param>
/// <param name="ActorEmail">Their e-mail at the time.</param>
/// <param name="OccurredAt">When (UTC).</param>
public record IncidentHistoryEntryResource(string Action, string Status, string? Detail, int? ActorUserId, string? ActorEmail,
    DateTimeOffset OccurredAt);

/// <summary>An incident of a room.</summary>
/// <param name="Id">Incident id.</param>
/// <param name="HotelId">Hotel of the room.</param>
/// <param name="RoomId">The room.</param>
/// <param name="Title">Short summary.</param>
/// <param name="Description">Details.</param>
/// <param name="Priority">Low, Medium, High or Critical.</param>
/// <param name="Status">Pending, InProgress or Resolved.</param>
/// <param name="BlocksRoom">The room is out of service until it is resolved.</param>
/// <param name="ReportedByUserId">Who reported it.</param>
/// <param name="ReportedByEmail">Their e-mail at the time.</param>
/// <param name="ReportedAt">When (UTC).</param>
/// <param name="AssigneeUserId">Maintenance staff member in charge.</param>
/// <param name="AssignedAt">When it was (last) assigned.</param>
/// <param name="StartedAt">When work started.</param>
/// <param name="ResolvedAt">When it was resolved.</param>
/// <param name="ResolvedByUserId">Who resolved it.</param>
/// <param name="ResolutionNotes">What was done.</param>
/// <param name="EscalatedAt">When the administrators were alerted that it was not started in time.</param>
/// <param name="History">What happened to it, oldest first (only when a single incident is read).</param>
/// <param name="Kind">Corrective (reported damage) or Preventive (generated by a preventive plan, US-30).</param>
/// <param name="PreventivePlanId">The preventive plan that generated it.</param>
/// <param name="IsRecurring">US-10: highlighted because the room accumulated similar incidents this month.</param>
/// <param name="RecurrenceCount">How many similar incidents of the room (this one included) were found.</param>
public record IncidentResource(int Id, int HotelId, int RoomId, string Title, string? Description, string Priority, string Status,
    bool BlocksRoom, int ReportedByUserId, string? ReportedByEmail, DateTimeOffset ReportedAt, int? AssigneeUserId,
    DateTimeOffset? AssignedAt, DateTimeOffset? StartedAt, DateTimeOffset? ResolvedAt, int? ResolvedByUserId,
    string? ResolutionNotes, DateTimeOffset? EscalatedAt, IReadOnlyList<IncidentHistoryEntryResource>? History,
    string Kind = "Corrective", int? PreventivePlanId = null, bool IsRecurring = false, int? RecurrenceCount = null);

/// <summary>Result of the escalation job.</summary>
/// <param name="Escalated">Critical incidents whose administrators were alerted in this run.</param>
public record IncidentEscalationsResultResource(int Escalated);

/// <summary>A new recurring preventive task of a room (US-30).</summary>
public record SchedulePreventiveMaintenanceResource
{
    /// <summary>The room.</summary>
    /// <example>12</example>
    [Required]
    public int? RoomId { get; init; }

    /// <summary>What to do (3-120 characters).</summary>
    /// <example>Limpiar filtros del aire acondicionado</example>
    [Required]
    public string? Title { get; init; }

    /// <summary>Details, optional (up to 1000 characters).</summary>
    /// <example>Retirar, lavar y secar los filtros; revisar el drenaje.</example>
    public string? Description { get; init; }

    /// <summary>Days between occurrences (1-365).</summary>
    /// <example>90</example>
    [Required]
    public int? FrequencyDays { get; init; }

    /// <summary>When the first task is generated (yyyy-MM-dd, today or later).</summary>
    /// <example>2026-12-01</example>
    [Required]
    public DateOnly? FirstDueOn { get; init; }

    /// <summary>Days before each due date the maintenance staff is reminded (0-30, shorter than the frequency; 3 by default).</summary>
    /// <example>3</example>
    public int? ReminderDaysBefore { get; init; }
}

/// <summary>New schedule of a preventive maintenance plan.</summary>
public record ReschedulePreventiveMaintenanceResource
{
    /// <summary>Days between occurrences (1-365).</summary>
    /// <example>60</example>
    [Required]
    public int? FrequencyDays { get; init; }

    /// <summary>When the next task is generated (yyyy-MM-dd, today or later).</summary>
    /// <example>2027-01-15</example>
    [Required]
    public DateOnly? NextDueOn { get; init; }

    /// <summary>Days before each due date the maintenance staff is reminded (0-30).</summary>
    /// <example>3</example>
    public int? ReminderDaysBefore { get; init; }
}

/// <summary>A recurring preventive task of a room (US-30).</summary>
/// <param name="Id">Plan id.</param>
/// <param name="HotelId">Hotel of the room.</param>
/// <param name="RoomId">The room.</param>
/// <param name="Title">What to do.</param>
/// <param name="Description">Details.</param>
/// <param name="FrequencyDays">Days between occurrences.</param>
/// <param name="NextDueOn">When the next task is generated.</param>
/// <param name="ReminderDaysBefore">Days before the due date the staff is reminded.</param>
/// <param name="IsActive">Still generating tasks.</param>
/// <param name="GeneratedCount">Tasks generated so far.</param>
/// <param name="LastIncidentId">The last task generated (an incident of kind Preventive).</param>
/// <param name="LastGeneratedAt">When it was generated.</param>
/// <param name="CreatedByUserId">Who scheduled it.</param>
/// <param name="CreatedAt">When.</param>
public record PreventiveMaintenancePlanResource(int Id, int HotelId, int RoomId, string Title, string? Description,
    int FrequencyDays, DateOnly NextDueOn, int ReminderDaysBefore, bool IsActive, int GeneratedCount, int? LastIncidentId,
    DateTimeOffset? LastGeneratedAt, int CreatedByUserId, DateTimeOffset CreatedAt);

/// <summary>Result of a run of the preventive maintenance job.</summary>
/// <param name="Generated">Tasks generated.</param>
/// <param name="Reminded">Plans whose maintenance staff was reminded.</param>
public record PreventiveMaintenanceRunResultResource(int Generated, int Reminded);

/// <summary>The maintenance history of a room (US-10, US-30).</summary>
/// <param name="RoomId">The room.</param>
/// <param name="CorrectiveCount">Incidents reported by the staff.</param>
/// <param name="PreventiveCount">Preventive tasks generated by its plans.</param>
/// <param name="RecurringCount">Incidents highlighted as part of a recurring pattern.</param>
/// <param name="Incidents">Every task, corrective and preventive, newest first, with dates and statuses.</param>
/// <param name="PreventivePlans">Its preventive plans.</param>
public record RoomMaintenanceHistoryResource(int RoomId, int CorrectiveCount, int PreventiveCount, int RecurringCount,
    IReadOnlyList<IncidentResource> Incidents, IReadOnlyList<PreventiveMaintenancePlanResource> PreventivePlans);

/// <summary>An automatic alert of an incident and how it was resolved (US-23).</summary>
/// <param name="Kind">Escalation (critical, not attended in time) or Recurrence (similar incidents of the room this month).</param>
/// <param name="RaisedAt">When the alert was raised.</param>
/// <param name="IncidentId">The incident.</param>
/// <param name="RoomId">Its room.</param>
/// <param name="Title">Its title.</param>
/// <param name="Priority">Its priority.</param>
/// <param name="Status">Its current status (Resolved: the alert was attended).</param>
/// <param name="ResolvedAt">When it was resolved.</param>
/// <param name="ResolvedByUserId">Who resolved it.</param>
/// <param name="ResolutionNotes">What was done.</param>
public record IncidentAlertResource(string Kind, DateTimeOffset RaisedAt, int IncidentId, int RoomId, string Title, string Priority,
    string Status, DateTimeOffset? ResolvedAt, int? ResolvedByUserId, string? ResolutionNotes);
