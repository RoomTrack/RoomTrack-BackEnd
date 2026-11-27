using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.ValueObjects;

namespace RoomTrack.Bookings.Domain.Model.Commands;

/// <summary>US-21: the guest evaluates their finished stay.</summary>
public record SubmitStayReviewCommand(int BookingId, BookingRequester Requester, StayRatings Ratings, string? Comment);
