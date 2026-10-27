using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Queries;
using RoomTrack.Bookings.Domain.Model.ValueObjects;

namespace RoomTrack.Bookings.Domain.Services;

/// <summary>
/// Defines the contract for services that handle booking queries.
/// </summary>
public interface IBookingQueryService
{
    /// <summary>The booking, or null when it does not exist or is not visible to the requester.</summary>
    Task<Booking?> Handle(GetBookingByIdQuery query);

    /// <summary>The bookings visible to the requester, newest first.</summary>
    Task<IEnumerable<Booking>> Handle(GetBookingsQuery query);

    /// <summary>One page of the bookings visible to the requester, with the total of the list.</summary>
    Task<BookingPage> Handle(GetBookingsPageQuery query);

    /// <summary>The bookings of a room visible to the requester.</summary>
    Task<IEnumerable<Booking>> Handle(GetBookingsByRoomIdQuery query);

    /// <summary>
    ///     The number of the room of each booking (one batch lookup in the Accommodations context), so clients never
    ///     resolve rooms themselves.
    /// </summary>
    Task<IReadOnlyDictionary<int, string>> FetchRoomNumbersAsync(IEnumerable<Booking> bookings);

    /// <summary>
    ///     How to pay <paramref name="booking"/>: the payment methods of its hotel while it is Pending;
    ///     null once it is paid or cancelled, or when the hotel has none.
    /// </summary>
    Task<Accommodations.Interfaces.ACL.HotelPaymentInstructions?> FetchPaymentInstructionsAsync(Booking booking);

    /// <summary>The calendar of a hotel.</summary>
    Task<BookingCalendar> Handle(GetBookingCalendarQuery query);
}
