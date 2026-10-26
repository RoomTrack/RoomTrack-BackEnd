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
public record IncidentResource(int Id, int HotelId, int RoomId, string Title, string? Description, string Priority, string Status,
    bool BlocksRoom, int ReportedByUserId, string? ReportedByEmail, DateTimeOffset ReportedAt, int? AssigneeUserId,
    DateTimeOffset? AssignedAt, DateTimeOffset? StartedAt, DateTimeOffset? ResolvedAt, int? ResolvedByUserId,
    string? ResolutionNotes, DateTimeOffset? EscalatedAt, IReadOnlyList<IncidentHistoryEntryResource>? History);

/// <summary>Result of the escalation job.</summary>
/// <param name="Escalated">Critical incidents whose administrators were alerted in this run.</param>
public record IncidentEscalationsResultResource(int Escalated);
