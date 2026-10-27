using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Model.Queries;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Bookings.Interfaces.ACL;

namespace RoomTrack.Bookings.Application.ACL;

public class BookingsContextFacade(
    IBookingQueryService bookingQueryService,
    IBookingCommandService bookingCommandService) : IBookingsContextFacade
{
    public async Task<BookingSnapshot?> FetchBookingAsync(int bookingId, int? guestUserId = null)
    {
        var requester = guestUserId.HasValue ? BookingRequester.Guest(guestUserId.Value, string.Empty) : null;
        var booking = await bookingQueryService.Handle(new GetBookingByIdQuery(bookingId, requester));
        return booking is null ? null : ToSnapshot(booking);
    }

    public async Task<bool> ConfirmBookingAsync(int bookingId)
    {
        await bookingCommandService.Handle(new ConfirmBookingCommand(bookingId));
        return true;
    }

    private static BookingSnapshot ToSnapshot(Booking booking) => new(
        booking.Id,
        booking.RoomId,
        booking.CheckInDate,
        booking.CheckOutDate,
        booking.Nights,
        booking.Status.ToString(),
        booking.CanBePaid,
        booking.HotelId,
        booking.Code.Value,
        booking.TotalPrice,
        booking.GuestEmail);
}
