using BackendAwRoomTrack.API.Accommodations.Interfaces.ACL;
using BackendAwRoomTrack.API.Bookings.Domain.Model.Aggregates;

namespace BackendAwRoomTrack.API.Bookings.Application.OutboundServices;

/// <summary>Where a booking is: its hotel and the number of its room (as guests and staff know it).</summary>
public sealed record BookingPlace(HotelSummary? Hotel, string RoomNumber);

/// <summary>
///     E-mails to the guest about their booking. Called by the booking event handlers, inside the transaction
///     of the change (the e-mails go through the outbox: stored only if the change commits).
/// </summary>
public interface IBookingNotificationService
{
    /// <summary>
    ///     Booking received with its code, total, how to pay (the hotel's payment methods; null
    ///     only for a hotel that has none, e.g. a booking made before they were required) and the payment deadline.
    /// </summary>
    Task SendBookingPlacedAsync(Booking booking, BookingPlace place, HotelPaymentInstructions? instructions);

    /// <summary>The payment was registered: the booking is confirmed.</summary>
    Task SendBookingConfirmedAsync(Booking booking, BookingPlace place);

    /// <summary>The booking was cancelled (by the guest, the hotel or the payment deadline).</summary>
    Task SendBookingCancelledAsync(Booking booking, BookingPlace place);

    /// <summary>The dates or the room changed.</summary>
    Task SendBookingRescheduledAsync(Booking booking, BookingPlace place, DateTime previousCheckIn, DateTime previousCheckOut, string previousRoomNumber);
}
