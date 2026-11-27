using RoomTrack.Bookings.Domain.Model.ValueObjects;

namespace RoomTrack.Bookings.Domain.Model.Commands;

/// <summary>
///     The guest completes the digital check-in of their booking with their identity document, or the front desk
///     registers it for a guest who arrives without having done it (US-27, assisted check-in).
/// </summary>
/// <param name="BookingId">The booking.</param>
/// <param name="Requester">The guest (owner of the booking) or a front desk staff member of its hotel.</param>
/// <param name="DocumentType">DNI, passport or carné de extranjería.</param>
/// <param name="DocumentNumber">Document number.</param>
/// <param name="Nationality">ISO 3166-1 alpha-2 code.</param>
/// <param name="DocumentContent">Photo or scan of the document (JPEG, PNG or PDF, ≤ 5 MB).</param>
public record CompleteDigitalCheckInCommand(int BookingId, BookingRequester Requester, IdentityDocumentType DocumentType,
    string DocumentNumber, string Nationality, byte[] DocumentContent);

/// <summary>The guest asks the front desk for help with the check-in.</summary>
public record RequestCheckInAssistanceCommand(int BookingId, BookingRequester Requester, string? Message);

/// <summary>
///     The stay ends: the guest checks out from the app (US-14) or the front desk does it for them (US-27).
/// </summary>
public record CheckOutCommand(int BookingId, BookingRequester Requester);

/// <summary>
///     The guest who did the digital check-in before arriving is at the hotel (US-13): they confirm their identity with
///     the number of the document they sent, and get the room access code. The front desk can confirm it for them.
/// </summary>
/// <param name="BookingId">The booking.</param>
/// <param name="Requester">The guest (owner of the booking) or a front desk staff member of its hotel.</param>
/// <param name="DocumentNumber">The number of the identity document sent with the digital check-in.</param>
public record ConfirmArrivalCommand(int BookingId, BookingRequester Requester, string DocumentNumber);
