namespace BackendAwRoomTrack.API.Bookings.Interfaces.ACL;

/// <summary>
///     Snapshot of a booking exposed to other bounded contexts (no domain entity leaks through the ACL).
/// </summary>
/// <param name="BookingId">The booking identifier.</param>
/// <param name="RoomId">The booked room.</param>
/// <param name="CheckInDate">Check-in date.</param>
/// <param name="CheckOutDate">Check-out date.</param>
/// <param name="Nights">Number of nights.</param>
/// <param name="Status">Pending, Confirmed, Cancelled or Completed.</param>
/// <param name="CanBePaid">True while the booking is Pending (waiting for its payment).</param>
/// <param name="HotelId">Hotel of the booked room.</param>
/// <param name="Code">Human-readable booking code.</param>
/// <param name="TotalPrice">Price per night agreed × nights: the amount to pay.</param>
/// <param name="GuestEmail">Where the booking e-mails go.</param>
public record BookingSnapshot(
    int BookingId,
    int RoomId,
    DateTime CheckInDate,
    DateTime CheckOutDate,
    int Nights,
    string Status,
    bool CanBePaid,
    int HotelId,
    string Code,
    decimal TotalPrice,
    string GuestEmail);

/// <summary>
///     Anti-corruption layer facade of the Bookings bounded context.
/// </summary>
public interface IBookingsContextFacade
{
    /// <summary>
    ///     Returns the booking snapshot. When <paramref name="guestUserId"/> is given, the booking is only
    ///     returned if that guest owns it (otherwise null, as if it did not exist).
    /// </summary>
    Task<BookingSnapshot?> FetchBookingAsync(int bookingId, int? guestUserId = null);

    /// <summary>
    ///     Confirms the booking because its payment was registered (D1), through the Bookings application layer.
    ///     Changes pending in the shared unit of work (the new payment) are committed together.
    /// </summary>
    /// <exception cref="BackendAwRoomTrack.Domain.Shared.Domain.Model.Exceptions.EntityNotFoundException">The booking does not exist.</exception>
    Task<bool> ConfirmBookingAsync(int bookingId);
}
