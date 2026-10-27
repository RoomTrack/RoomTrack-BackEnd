using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.ValueObjects;

namespace RoomTrack.Bookings.Domain.Model.Queries;

/// <summary>One page of <see cref="GetBookingsQuery"/>: the same bookings, newest first.</summary>
public record GetBookingsPageQuery(BookingRequester Requester, PageRequest Page);

/// <summary>The bookings of a page and how many bookings the whole list has.</summary>
public record BookingPage(IReadOnlyList<Booking> Items, int TotalCount);
