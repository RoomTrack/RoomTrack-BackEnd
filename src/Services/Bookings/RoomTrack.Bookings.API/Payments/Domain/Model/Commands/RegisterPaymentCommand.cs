using RoomTrack.Payments.Domain.Model.ValueObjects;

namespace RoomTrack.Payments.Domain.Model.Commands;

/// <summary>
///     Hotel staff register the payment of a booking. The amount is not part of the command: it is
///     always the booking's total.
/// </summary>
/// <param name="BookingId">The booking paid.</param>
/// <param name="Method">How it was paid.</param>
/// <param name="OperationNumber">Operation number (required except for cash).</param>
/// <param name="Note">Optional note.</param>
/// <param name="StaffUserId">Who registers it.</param>
/// <param name="StaffHotelId">Their hotel (admin, reception).</param>
/// <param name="AllHotels">True for a chain administrator.</param>
public record RegisterPaymentCommand(
    int BookingId,
    PaymentMethod Method,
    string? OperationNumber,
    string? Note,
    int StaffUserId,
    int? StaffHotelId,
    bool AllHotels);

/// <summary>
///     The guest pays their Pending booking from the app with a card (US-15): the card gateway charges the booking
///     total to the card behind <paramref name="CardToken"/>.
/// </summary>
/// <param name="BookingId">The booking paid.</param>
/// <param name="GuestUserId">The guest, owner of the booking.</param>
/// <param name="CardToken">The token of the card, issued by the gateway to the app (the API never sees the card).</param>
public record PayBookingByCardCommand(int BookingId, int GuestUserId, string CardToken);
