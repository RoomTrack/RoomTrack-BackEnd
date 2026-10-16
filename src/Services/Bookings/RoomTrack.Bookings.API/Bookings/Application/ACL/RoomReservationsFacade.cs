using BackendAwRoomTrack.API.Bookings.Domain.Model.Queries;
using BackendAwRoomTrack.API.Bookings.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Bookings.Domain.Repositories;
using BackendAwRoomTrack.API.Bookings.Domain.Services;
using BackendAwRoomTrack.API.Bookings.Interfaces.ACL;

namespace BackendAwRoomTrack.API.Bookings.Application.ACL;

public class RoomReservationsFacade(IBookingRepository bookingRepository, IBookingQueryService bookingQueryService)
    : IRoomReservationsFacade
{
    public Task<IReadOnlyDictionary<int, int>> CountActiveBookingsAsync(IReadOnlyCollection<int> roomIds) =>
        bookingRepository.CountActiveByRoomAsync(roomIds);

    public async Task<bool> HasCurrentConfirmedStayAsync(int guestUserId, int roomId, DateTime day)
    {
        var requester = BookingRequester.Guest(guestUserId, string.Empty);
        // Only the guest's own bookings (account or guest profile) are returned by the query.
        var bookings = await bookingQueryService.Handle(new GetBookingsQuery(requester));
        return bookings.Any(booking => booking.IsConfirmedStayIn(roomId, day));
    }
}
