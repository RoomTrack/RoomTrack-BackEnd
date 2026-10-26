using RoomTrack.Bookings.Domain.Model.ValueObjects;

namespace RoomTrack.Bookings.Domain.Model.Queries;

/// <summary>The bookings visible to the requester (a guest: their own; hotel staff: all), newest first.</summary>
public record GetBookingsQuery(BookingRequester Requester);
