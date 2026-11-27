using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Exceptions;
using RoomTrack.Bookings.Domain.Model.Queries;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Domain.Repositories;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Profiles.Interfaces.ACL;

namespace RoomTrack.Bookings.Application.Internal.QueryServices;

public class StayReviewQueryService(
    IBookingRepository bookingRepository,
    IStayReviewRepository reviewRepository,
    IGuestProfilesContextFacade guestProfilesContextFacade) : IStayReviewQueryService
{
    public const int MaxLimit = 5000;

    public async Task<StayReview?> Handle(GetStayReviewByBookingIdQuery query)
    {
        var requester = query.Requester.IsGuest
            ? query.Requester.WithGuestProfile(
                await guestProfilesContextFacade.FetchGuestProfileIdByUserIdAsync(query.Requester.UserId))
            : query.Requester;
        var booking = await bookingRepository.FindByIdAsync(query.BookingId);
        if (booking is null || !booking.IsVisibleTo(requester)) throw new BookingNotFoundException(query.BookingId);
        return await reviewRepository.FindByBookingIdAsync(booking.Id);
    }

    public Task<IReadOnlyList<StayReview>> Handle(GetStayReviewsQuery query) =>
        reviewRepository.ListAsync(query.HotelId, query.From, query.To, Math.Clamp(query.Limit, 1, MaxLimit));

    public async Task<SatisfactionReport> Handle(GetStaySatisfactionQuery query)
    {
        var reviews = await reviewRepository.ListAsync(query.HotelId, query.From, query.To);
        return StaySatisfactionCalculator.Summarize(query.HotelId, query.From, query.To, reviews);
    }
}
