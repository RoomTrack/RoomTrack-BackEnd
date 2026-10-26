using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Model.ValueObjects;

namespace RoomTrack.Bookings.Domain.Services;

/// <summary>A completed check-in with the access code in clear (only for its guest).</summary>
public sealed record CheckInResult(Booking Booking, DigitalCheckIn CheckIn, RoomAccessCode? AccessCode);

/// <summary>Digital check-in use cases.</summary>
public interface ICheckInService
{
    Task<CheckInResult> Handle(CompleteDigitalCheckInCommand command);

    Task Handle(RequestCheckInAssistanceCommand command);

    /// <summary>Checks the guest out: the booking is Completed, the access code stops working and the room goes to cleaning.</summary>
    Task<Booking> Handle(CheckOutCommand command);

    /// <summary>
    ///     For a guest whose room is not ready yet (US-27): the rooms of the hotel that are ready now and free until the
    ///     check-out, same room type and same price first (a paid booking can move to a room with the same total).
    /// </summary>
    Task<IReadOnlyList<AvailableRoom>> FindAlternativeRoomsAsync(int bookingId, BookingRequester requester);

    /// <summary>The check-in of a booking visible to the requester, or null. The code is only revealed to the guest.</summary>
    Task<CheckInResult?> FindAsync(int bookingId, BookingRequester requester);
}
