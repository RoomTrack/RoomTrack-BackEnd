using RoomTrack.Bookings.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Interfaces.ASP.Validation;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RoomTrack.Bookings.Domain.Model.ValueObjects;

namespace RoomTrack.Bookings.Interfaces.REST.Resources;

/// <summary>The digital check-in form (multipart/form-data).</summary>
public class CheckInFormResource : IValidatableObject
{
    /// <summary>DNI, PASSPORT or CE (carné de extranjería).</summary>
    /// <example>DNI</example>
    [Required]
    [FromForm(Name = "documentType")]
    public string? DocumentType { get; init; }

    /// <summary>Document number: DNI 8 digits; passport 6–12 letters/digits; CE 8–12 letters/digits.</summary>
    /// <example>45678912</example>
    [Required, MaxLength(20)]
    [FromForm(Name = "documentNumber")]
    public string? DocumentNumber { get; init; }

    /// <summary>Nationality, ISO 3166-1 alpha-2 (PE, AR, US...). DNI only with PE; CE never with PE.</summary>
    /// <example>PE</example>
    [Required, MaxLength(2, ErrorMessage = "Nationality must be a 2-letter country code.")]
    [FromForm(Name = "nationality")]
    public string? Nationality { get; init; }

    /// <summary>Photo or scan of the document: JPG, PNG or PDF, at most 5 MB.</summary>
    [Required(ErrorMessage = "Upload a photo or scan of your identity document.")]
    [FromForm(Name = "document")]
    public IFormFile? Document { get; init; }

    public IdentityDocumentType ToDocumentType() => DocumentType!.Trim().ToUpperInvariant() switch
    {
        "DNI" => IdentityDocumentType.Dni,
        "PASSPORT" => IdentityDocumentType.Passport,
        _ => IdentityDocumentType.Ce
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(DocumentType) && DocumentType.Trim().ToUpperInvariant() is not ("DNI" or "PASSPORT" or "CE"))
            yield return new CodedValidationResult(BookingErrorCodes.CheckInDocumentTypeUnknown,
                "Document type must be DNI, PASSPORT or CE.", ["documentType"]);
    }
}

/// <summary>A request for help with the check-in.</summary>
public record CheckInAssistanceResource
{
    /// <summary>What the guest needs (optional).</summary>
    /// <example>The app does not accept my passport photo.</example>
    [MaxLength(500)]
    public string? Message { get; init; }
}

/// <summary>The guest at the hotel confirms their identity (US-13).</summary>
public record ConfirmArrivalResource
{
    /// <summary>The number of the identity document sent with the digital check-in.</summary>
    /// <example>45678912</example>
    [Required(ErrorMessage = "Enter the number of the identity document you sent with the digital check-in."), MaxLength(20)]
    public string? DocumentNumber { get; init; }
}

/// <summary>A digital check-in: completed, or done before the arrival and waiting for the guest (US-13).</summary>
/// <param name="BookingId">The booking.</param>
/// <param name="BookingCode">Its code.</param>
/// <param name="BookingStatus">CheckedIn (completed) or Confirmed (waiting for the arrival).</param>
/// <param name="RoomId">The room of the stay.</param>
/// <param name="Status">Approved (completed) or PendingArrival ("check-in en proceso": done before arriving).</param>
/// <param name="DocumentType">DNI, PASSPORT or CE.</param>
/// <param name="DocumentNumberMasked">Document number with all but the last 3 characters hidden.</param>
/// <param name="Nationality">ISO country code.</param>
/// <param name="AccessCode">6-digit room access code. Only for the guest of a completed check-in; null otherwise.</param>
/// <param name="AccessCodeValidUntil">The code works until the check-out (UTC); null while waiting for the arrival.</param>
/// <param name="SubmittedAt">When the guest sent the document (UTC).</param>
/// <param name="CompletedAt">When the check-in was completed (UTC); null while waiting for the arrival.</param>
public record CheckInResource(int BookingId, string BookingCode, string BookingStatus, int RoomId, string Status,
    string DocumentType, string DocumentNumberMasked, string Nationality, string? AccessCode,
    DateTimeOffset? AccessCodeValidUntil, DateTimeOffset SubmittedAt, DateTimeOffset? CompletedAt);
