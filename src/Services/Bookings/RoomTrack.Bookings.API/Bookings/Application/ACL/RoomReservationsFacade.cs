using RoomTrack.Bookings.Application.Internal.Configuration;
using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Queries;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Domain.Repositories;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Bookings.Interfaces.ACL;

namespace RoomTrack.Bookings.Application.ACL;

public class RoomReservationsFacade(IBookingRepository bookingRepository, IBookingQueryService bookingQueryService,
    HotelCalendar calendar)
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

    public async Task<GuestStay?> FetchCurrentStayAsync(int guestUserId) =>
        CurrentStayOf(await bookingQueryService.Handle(new GetBookingsQuery(BookingRequester.Guest(guestUserId, string.Empty))));

    public async Task<GuestStay?> FetchCurrentStayOfRoomAsync(int roomId) =>
        CurrentStayOf(await bookingRepository.FindByRoomIdAsync(roomId));

    /// <summary>The stay in progress first; otherwise a paid booking that covers today (not checked in yet).</summary>
    private GuestStay? CurrentStayOf(IEnumerable<Booking> bookings)
    {
        var today = calendar.Today;
        var stay = bookings
            .Where(booking => booking.Status == BookingStatus.CheckedIn
                              || (booking.Status == BookingStatus.Confirmed && booking.Dates.Includes(today)))
            .OrderByDescending(booking => booking.Status == BookingStatus.CheckedIn)
            .ThenBy(booking => booking.CheckInDate)
            .FirstOrDefault();
        return stay is null
            ? null
            : new GuestStay(stay.Id, stay.Code.Value, stay.HotelId, stay.RoomId, stay.GuestId?.Value, stay.CheckInDate,
                stay.CheckOutDate, stay.Status == BookingStatus.CheckedIn);
    }
}
