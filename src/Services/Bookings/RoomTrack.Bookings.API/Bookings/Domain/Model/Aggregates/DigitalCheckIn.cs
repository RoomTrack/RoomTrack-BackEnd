using RoomTrack.Bookings.Domain.Model.Exceptions;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Bookings.Domain.Model.Aggregates;

/// <summary>Where the digital check-in is.</summary>
public enum CheckInStatus
{
    /// <summary>The document passed the format validation and the guest is in: the check-in is completed.</summary>
    Approved,

    /// <summary>
    ///     "Check-in en proceso" (US-13): the guest did the digital check-in before arriving and the document passed the
    ///     format validation; at the hotel they only confirm their identity to get the access code.
    /// </summary>
    PendingArrival
}

/// <summary>
///     The digital check-in of a booking: the identity declared by the guest, the reference to the uploaded
///     document (stored encrypted outside the aggregate) and the room access code (stored encrypted). One per booking.
/// </summary>
/// <remarks>
///     Done on the check-in day, it is approved at once. Done before the arrival (US-13), it waits for the guest
///     (<see cref="CheckInStatus.PendingArrival"/>): when they arrive they confirm their identity with the number of the
///     same document (<see cref="ConfirmArrival"/>) and get the access code.
/// </remarks>
public class DigitalCheckIn
{
    /// <summary>EF Core constructor.</summary>
    protected DigitalCheckIn()
    {
        DocumentNumber = string.Empty;
        Nationality = string.Empty;
        DocumentFileId = string.Empty;
        DocumentContentType = string.Empty;
        AccessCodeProtected = string.Empty;
    }

    public int Id { get; private set; }
    public int BookingId { get; private set; }
    public int? GuestUserId { get; private set; }
    public IdentityDocumentType DocumentType { get; private set; }
    public string DocumentNumber { get; private set; }
    public string Nationality { get; private set; }

    /// <summary>Key of the uploaded document in the document storage.</summary>
    public string DocumentFileId { get; private set; }
    public string DocumentContentType { get; private set; }
    public long DocumentSizeBytes { get; private set; }

    public CheckInStatus Status { get; private set; }

    /// <summary>The room access code, encrypted by the application (empty until the guest arrives).</summary>
    public string AccessCodeProtected { get; private set; }

    /// <summary>Until when the access code works (the check-out); null until the guest arrives.</summary>
    public DateTimeOffset? AccessCodeValidUntil { get; private set; }

    /// <summary>When the guest sent the document (the digital check-in itself).</summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>When the check-in was completed (the guest is in); null while it waits for the arrival.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsAwaitingArrival => Status == CheckInStatus.PendingArrival;

    public GuestIdentityDocument Identity => new(DocumentType, DocumentNumber, Nationality);

    /// <summary>The guest checked out: the room access code stops working now (it was valid until the check-out).</summary>
    public void EndAccess(DateTimeOffset now)
    {
        if (AccessCodeValidUntil is { } until && now < until) AccessCodeValidUntil = now;
    }

    /// <summary>
    ///     Records the approved check-in of <paramref name="booking"/>: the document passed the format validation
    ///     (<see cref="GuestIdentityDocument"/>, <see cref="IdentityDocumentFile"/>) and the stay starts.
    /// </summary>
    public static DigitalCheckIn Approve(Booking booking, GuestIdentityDocument identity, IdentityDocumentFile file,
        string documentFileId, string accessCodeProtected, DateTimeOffset accessCodeValidUntil, DateTimeOffset now)
    {
        var checkIn = Submit(booking, identity, file, documentFileId, CheckInStatus.Approved, now);
        checkIn.GrantAccess(accessCodeProtected, accessCodeValidUntil, now);
        return checkIn;
    }

    /// <summary>
    ///     The guest does the digital check-in before arriving (US-13): the document passed the format validation and the
    ///     check-in waits for them ("check-in en proceso"). No access code yet: the room is not theirs until they arrive.
    /// </summary>
    public static DigitalCheckIn PreRegister(Booking booking, GuestIdentityDocument identity, IdentityDocumentFile file,
        string documentFileId, DateTimeOffset now) =>
        Submit(booking, identity, file, documentFileId, CheckInStatus.PendingArrival, now);

    /// <summary>
    ///     The guest who did the check-in before arriving is at the hotel (US-13): they only confirm their identity with
    ///     the number of the document they sent, and get the access code.
    /// </summary>
    /// <exception cref="InvalidFieldException">The number is not the one of the document sent.</exception>
    public void ConfirmArrival(string? documentNumber, string accessCodeProtected, DateTimeOffset accessCodeValidUntil, DateTimeOffset now)
    {
        if (!IsAwaitingArrival)
            throw new BusinessRuleViolationException(BookingErrorCodes.CheckInNotAwaitingArrival,
                "This check-in is not waiting for the guest's arrival: it is already completed.");
        if (!Identity.HasNumber(documentNumber))
            throw new InvalidFieldException("documentNumber", BookingErrorCodes.CheckInIdentityMismatch,
                "The document number does not match the document sent with the digital check-in.");
        Status = CheckInStatus.Approved;
        GrantAccess(accessCodeProtected, accessCodeValidUntil, now);
    }

    private static DigitalCheckIn Submit(Booking booking, GuestIdentityDocument identity, IdentityDocumentFile file,
        string documentFileId, CheckInStatus status, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(documentFileId))
            throw new DomainValidationException(BookingErrorCodes.InternalInvariant, "The check-in needs the stored document.");

        return new DigitalCheckIn
        {
            BookingId = booking.Id,
            GuestUserId = booking.GuestId?.Value,
            DocumentType = identity.Type,
            DocumentNumber = identity.Number,
            Nationality = identity.Nationality,
            DocumentFileId = documentFileId,
            DocumentContentType = file.ContentType,
            DocumentSizeBytes = file.SizeBytes,
            Status = status,
            SubmittedAt = now
        };
    }

    private void GrantAccess(string accessCodeProtected, DateTimeOffset accessCodeValidUntil, DateTimeOffset now)
    {
        if (accessCodeValidUntil <= now)
            throw new DomainValidationException(BookingErrorCodes.InternalInvariant, "The access code must be valid until the check-out.");
        AccessCodeProtected = accessCodeProtected;
        AccessCodeValidUntil = accessCodeValidUntil;
        CompletedAt = now;
    }
}
