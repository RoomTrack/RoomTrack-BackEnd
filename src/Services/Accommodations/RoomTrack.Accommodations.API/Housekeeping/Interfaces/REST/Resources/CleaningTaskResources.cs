using System.ComponentModel.DataAnnotations;
using BackendAwRoomTrack.API.Housekeeping.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Incidents.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Shared.Infrastructure.Interfaces.ASP.Validation;
using BackendAwRoomTrack.Domain.Shared.Domain.Model.Exceptions;

namespace BackendAwRoomTrack.API.Housekeeping.Interfaces.REST.Resources;

/// <summary>A new cleaning task of a room.</summary>
public record CreateCleaningTaskResource : IValidatableObject
{
    /// <summary>The room to clean.</summary>
    /// <example>12</example>
    [Required]
    public int? RoomId { get; init; }

    /// <summary>Low, Normal, High or Urgent (case-insensitive; Normal by default).</summary>
    /// <example>High</example>
    public string? Priority { get; init; }

    /// <summary>Instructions for housekeeping, optional (up to 500 characters).</summary>
    /// <example>Guest arrives at 15:00, add an extra bed.</example>
    public string? Notes { get; init; }

    /// <summary>
    ///     A housekeeping staff member of the room's hotel (IAM user id). Without it the task is assigned
    ///     automatically when the hotel has the automatic assignment on, and left unassigned otherwise.
    /// </summary>
    /// <example>7</example>
    public int? AssigneeUserId { get; init; }

    public CleaningTaskPriority ToPriority() => CleaningTaskPriorityValidation.Parse(Priority);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        CleaningTaskPriorityValidation.Validate(Priority);
}

/// <summary>Who takes care of a cleaning task.</summary>
public record AssignCleaningTaskResource
{
    /// <summary>
    ///     A housekeeping staff member of the task's hotel (IAM user id). Omit it (or send null) to assign it to the
    ///     least loaded one.
    /// </summary>
    /// <example>7</example>
    public int? AssigneeUserId { get; init; }
}

/// <summary>A damage found while cleaning, reported as an incident for maintenance.</summary>
public record ReportCleaningImpedimentResource : IValidatableObject
{
    /// <summary>Short summary (3-120 characters).</summary>
    /// <example>Broken bathroom mirror</example>
    [Required]
    public string? Title { get; init; }

    /// <summary>Details, optional (up to 1000 characters).</summary>
    public string? Description { get; init; }

    /// <summary>Urgency of the incident: Low, Medium, High or Critical (case-insensitive; Medium by default).</summary>
    /// <example>High</example>
    public string? Priority { get; init; }

    /// <summary>The room cannot be used until the incident is resolved: it goes to Maintenance meanwhile.</summary>
    /// <example>true</example>
    public bool BlocksRoom { get; init; }

    public IncidentPriority ToPriority() =>
        string.IsNullOrWhiteSpace(Priority) ? IncidentPriority.Medium : Enum.Parse<IncidentPriority>(Priority, ignoreCase: true);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Priority)
            && (!Enum.TryParse<IncidentPriority>(Priority, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed) || int.TryParse(Priority, out _)))
            yield return new CodedValidationResult(ErrorCodes.FieldNotAllowed,
                $"Priority must be one of: {string.Join(", ", Enum.GetNames<IncidentPriority>())}.", ["priority"],
                new Dictionary<string, object?> { ["allowed"] = Enum.GetNames<IncidentPriority>() });
    }
}

/// <summary>New urgency of a cleaning task.</summary>
public record ChangeCleaningTaskPriorityResource : IValidatableObject
{
    /// <summary>Low, Normal, High or Urgent (case-insensitive).</summary>
    /// <example>Urgent</example>
    [Required]
    public string? Priority { get; init; }

    public CleaningTaskPriority ToPriority() => CleaningTaskPriorityValidation.Parse(Priority);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        CleaningTaskPriorityValidation.Validate(Priority);
}

/// <summary>Why a cleaning task is cancelled.</summary>
public record CancelCleaningTaskResource
{
    /// <summary>Optional (up to 500 characters).</summary>
    /// <example>Created for the wrong room.</example>
    public string? Reason { get; init; }
}

/// <summary>How a hotel distributes its cleaning tasks.</summary>
public record UpdateHousekeepingSettingsResource
{
    /// <summary>New tasks without an assignee go to the housekeeping staff member with the fewest open tasks.</summary>
    /// <example>true</example>
    [Required]
    public bool? AutoAssign { get; init; }
}

internal static class CleaningTaskPriorityValidation
{
    public static CleaningTaskPriority Parse(string? priority) =>
        string.IsNullOrWhiteSpace(priority) ? CleaningTaskPriority.Normal : Enum.Parse<CleaningTaskPriority>(priority, ignoreCase: true);

    public static IEnumerable<ValidationResult> Validate(string? priority)
    {
        if (!string.IsNullOrWhiteSpace(priority)
            && (!Enum.TryParse<CleaningTaskPriority>(priority, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed) || int.TryParse(priority, out _)))
            yield return new CodedValidationResult(ErrorCodes.FieldNotAllowed,
                $"Priority must be one of: {string.Join(", ", Enum.GetNames<CleaningTaskPriority>())}.", ["priority"],
                new Dictionary<string, object?> { ["allowed"] = Enum.GetNames<CleaningTaskPriority>() });
    }
}

/// <summary>One line of the history of a cleaning task.</summary>
/// <param name="Action">Created, Assigned, Reassigned, Started, PriorityChanged, ImpedimentReported, Completed or Cancelled.</param>
/// <param name="Status">Status of the task after it.</param>
/// <param name="Detail">What changed (assignee, priority, impediment title, cancellation reason).</param>
/// <param name="IncidentId">The incident reported for maintenance (ImpedimentReported only).</param>
/// <param name="ActorUserId">Who (null for the system).</param>
/// <param name="ActorEmail">Their e-mail at the time.</param>
/// <param name="OccurredAt">When (UTC).</param>
public record CleaningTaskHistoryEntryResource(string Action, string Status, string? Detail, int? IncidentId, int? ActorUserId,
    string? ActorEmail, DateTimeOffset OccurredAt);

/// <summary>A cleaning task of a room.</summary>
/// <param name="Id">Task id.</param>
/// <param name="HotelId">Hotel of the room.</param>
/// <param name="RoomId">The room.</param>
/// <param name="Priority">Low, Normal, High or Urgent.</param>
/// <param name="Status">Pending, InProgress, Completed or Cancelled.</param>
/// <param name="Notes">Instructions for housekeeping.</param>
/// <param name="CreatedByUserId">Who created it (null: the system, when the room went to Cleaning).</param>
/// <param name="CreatedAt">When (UTC).</param>
/// <param name="AssigneeUserId">Housekeeping staff member in charge.</param>
/// <param name="AssignedAt">When it was (last) assigned.</param>
/// <param name="AssignedAutomatically">The last assignment was the automatic one (least workload).</param>
/// <param name="StartedAt">When cleaning started.</param>
/// <param name="CompletedAt">When cleaning finished.</param>
/// <param name="CompletedByUserId">Who finished it.</param>
/// <param name="CancelledAt">When it was cancelled.</param>
/// <param name="CancellationReason">Why.</param>
/// <param name="ImpedimentIncidentIds">Incidents reported for maintenance while cleaning.</param>
/// <param name="History">What happened to it, oldest first (only when a single task is read).</param>
public record CleaningTaskResource(int Id, int HotelId, int RoomId, string Priority, string Status, string? Notes,
    int? CreatedByUserId, DateTimeOffset CreatedAt, int? AssigneeUserId, DateTimeOffset? AssignedAt, bool AssignedAutomatically,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, int? CompletedByUserId, DateTimeOffset? CancelledAt,
    string? CancellationReason, IReadOnlyList<int>? ImpedimentIncidentIds, IReadOnlyList<CleaningTaskHistoryEntryResource>? History);

/// <summary>How a hotel distributes its cleaning tasks.</summary>
/// <param name="HotelId">The hotel.</param>
/// <param name="AutoAssign">New tasks without an assignee go to the least loaded housekeeping staff member.</param>
/// <param name="UpdatedAt">When it was last changed (null: never configured).</param>
/// <param name="UpdatedByUserId">Who changed it.</param>
public record HousekeepingSettingsResource(int HotelId, bool AutoAssign, DateTimeOffset? UpdatedAt, int? UpdatedByUserId);
