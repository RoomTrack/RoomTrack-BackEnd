using RoomTrack.Bookings.Domain.Model.ValueObjects;

namespace RoomTrack.Bookings.Domain.Model.Queries;

/// <summary>The evaluation of a stay, for its guest or the staff of its hotel.</summary>
public record GetStayReviewByBookingIdQuery(int BookingId, BookingRequester Requester);

/// <summary>The evaluations of a hotel submitted in [From, To), newest first.</summary>
public record GetStayReviewsQuery(int HotelId, DateTimeOffset From, DateTimeOffset To, int Limit = 50);

/// <summary>US-26: the satisfaction of the guests of a hotel by area, from the evaluations submitted in [From, To).</summary>
public record GetStaySatisfactionQuery(int HotelId, DateTimeOffset From, DateTimeOffset To);
