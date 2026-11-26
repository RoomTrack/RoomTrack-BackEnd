using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Interfaces.ASP.Validation;
using System.ComponentModel.DataAnnotations;
using RoomTrack.ShiftHandovers.Domain.Model.ValueObjects;

namespace RoomTrack.ShiftHandovers.Interfaces.REST.Resources;

/// <summary>The end of a shift (US-32).</summary>
public record CreateShiftHandoverResource : IValidatableObject
{
    /// <summary>What the next shift must know (up to 2000 characters).</summary>
    /// <example>La 204 pidió almohadas extra para las 20:00. La 310 sigue con olor a humedad, avisé a mantenimiento.</example>
    public string? Notes { get; init; }

    /// <summary>
    ///     FrontDesk, Housekeeping or Maintenance. Only an administrator names it; staff hand over the area of their role.
    /// </summary>
    /// <example>Housekeeping</example>
    public string? Area { get; init; }

    public ShiftArea? ToArea() => string.IsNullOrWhiteSpace(Area) ? null : Enum.Parse<ShiftArea>(Area, ignoreCase: true);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Area)
            && (int.TryParse(Area, out _) || !Enum.TryParse<ShiftArea>(Area, true, out var parsed) || !Enum.IsDefined(parsed)))
            yield return new CodedValidationResult(ErrorCodes.FieldNotAllowed,
                $"Area must be one of: {string.Join(", ", Enum.GetNames<ShiftArea>())}.", ["area"],
                new Dictionary<string, object?> { ["allowed"] = Enum.GetNames<ShiftArea>() });
    }
}

/// <summary>A task or incident open when the shift ended.</summary>
/// <param name="Kind">CleaningTask or Incident.</param>
/// <param name="ReferenceId">Its id (<c>/cleaning-tasks/{id}</c> or <c>/incidents/{id}</c>).</param>
/// <param name="RoomId">The room.</param>
/// <param name="Summary">Title or notes.</param>
/// <param name="Status">Its status at the end of the shift.</param>
/// <param name="Priority">Its priority at the end of the shift.</param>
/// <param name="AssigneeUserId">Who had it.</param>
public record ShiftHandoverItemResource(string Kind, int ReferenceId, int RoomId, string Summary, string Status, string Priority,
    int? AssigneeUserId);

/// <summary>Who confirmed reading the handover, and when.</summary>
/// <param name="UserId">The reader.</param>
/// <param name="Email">Their e-mail.</param>
/// <param name="AcknowledgedAt">When (UTC).</param>
public record ShiftHandoverAcknowledgementResource(int UserId, string? Email, DateTimeOffset AcknowledgedAt);

/// <summary>A shift handover (US-32).</summary>
/// <param name="Id">Handover id.</param>
/// <param name="HotelId">The hotel.</param>
/// <param name="Area">FrontDesk, Housekeeping or Maintenance.</param>
/// <param name="AuthorUserId">Who ended the shift.</param>
/// <param name="AuthorEmail">Their e-mail.</param>
/// <param name="Notes">Their notes.</param>
/// <param name="CreatedAt">When (UTC).</param>
/// <param name="Items">Tasks and incidents open at the end of the shift.</param>
/// <param name="Acknowledgements">Who confirmed reading it.</param>
/// <param name="AcknowledgedByMe">The requester already confirmed it.</param>
public record ShiftHandoverResource(int Id, int HotelId, string Area, int AuthorUserId, string? AuthorEmail, string? Notes,
    DateTimeOffset CreatedAt, IReadOnlyList<ShiftHandoverItemResource> Items,
    IReadOnlyList<ShiftHandoverAcknowledgementResource> Acknowledgements, bool AcknowledgedByMe);
