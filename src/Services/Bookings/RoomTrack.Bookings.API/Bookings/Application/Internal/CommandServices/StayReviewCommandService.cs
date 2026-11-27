using RoomTrack.Bookings.Application.Internal.Configuration;
using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Commands;
using RoomTrack.Bookings.Domain.Model.Exceptions;
using RoomTrack.Bookings.Domain.Repositories;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Profiles.Interfaces.ACL;
using RoomTrack.Shared.Domain.Repositories;

namespace RoomTrack.Bookings.Application.Internal.CommandServices;

/// <summary>US-21: the guest evaluates their finished stay; the evaluation is stored with the booking.</summary>
public class StayReviewCommandService(
    IBookingRepository bookingRepository,
    IStayReviewRepository reviewRepository,
    IGuestProfilesContextFacade guestProfilesContextFacade,
    HotelCalendar calendar,
    IUnitOfWork unitOfWork) : IStayReviewCommandService
{
    public async Task<StayReview> Handle(SubmitStayReviewCommand command)
    {
        // A booking made at the desk belongs to the guest through their guest profile.
        var requester = command.Requester.IsGuest
            ? command.Requester.WithGuestProfile(
                await guestProfilesContextFacade.FetchGuestProfileIdByUserIdAsync(command.Requester.UserId))
            : command.Requester;
        var booking = await bookingRepository.FindByIdAsync(command.BookingId)
                      ?? throw new BookingNotFoundException(command.BookingId);

        var review = StayReview.Submit(booking, requester, command.Ratings, command.Comment, calendar.Now);
        if (await reviewRepository.FindByBookingIdAsync(booking.Id) is not null)
            throw new StayReviewAlreadySubmittedException(booking.Id);

        await reviewRepository.AddAsync(review);
        await unitOfWork.CompleteAsync();
        return review;
    }
}
