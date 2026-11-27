using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Model.Queries;
using RoomTrack.Bookings.Domain.Model.ValueObjects;

namespace RoomTrack.Bookings.Domain.Services;

public interface IStayReviewCommandService
{
    Task<StayReview> Handle(SubmitStayReviewCommand command);
}

public interface IStayReviewQueryService
{
    /// <returns>The evaluation, or null when the stay was not evaluated.</returns>
    /// <exception cref="Model.Exceptions.BookingNotFoundException">The booking does not exist or the requester cannot see it.</exception>
    Task<StayReview?> Handle(GetStayReviewByBookingIdQuery query);

    Task<IReadOnlyList<StayReview>> Handle(GetStayReviewsQuery query);

    Task<SatisfactionReport> Handle(GetStaySatisfactionQuery query);
}
