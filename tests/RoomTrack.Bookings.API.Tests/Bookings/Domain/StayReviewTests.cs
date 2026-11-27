using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.Exceptions;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Domain.Services;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Xunit;
using static RoomTrack.Tests.Bookings.BookingTestData;

namespace RoomTrack.Tests.Bookings.Domain;

/// <summary>Rules of the evaluation of a stay (US-21) and of the satisfaction by area (US-26).</summary>
public class StayReviewTests
{
    private static readonly DateTimeOffset CheckedOutAt = new(2030, 1, 13, 17, 0, 0, TimeSpan.Zero);
    private static readonly BookingRequester Guest = BookingRequester.Guest(GuestUserId, "alice@example.com");

    [Fact]
    public void TheGuest_ShouldEvaluateTheirFinishedStay()
    {
        var booking = FinishedStay(CheckedOutAt);

        var review = StayReview.Submit(booking, Guest, new StayRatings(4, FrontDesk: 5, Housekeeping: 3),
            "  La ducha tardaba en calentar  ", CheckedOutAt.AddHours(2));

        (review.HotelId, review.RoomId, review.GuestUserId).Should().Be((booking.HotelId, booking.RoomId, GuestUserId));
        (review.OverallRating, review.FrontDeskRating, review.HousekeepingRating, review.MaintenanceRating)
            .Should().Be((4, 5, 3, (int?)null));
        review.Comment.Should().Be("La ducha tardaba en calentar");
        review.SubmittedAt.Should().Be(CheckedOutAt.AddHours(2));
    }

    [Fact]
    public void AnotherGuest_ShouldNotEvaluateTheStay()
    {
        var submit = () => StayReview.Submit(FinishedStay(CheckedOutAt), BookingRequester.Guest(99, "bob@example.com"),
            new StayRatings(1), null, CheckedOutAt);

        submit.Should().Throw<BookingNotFoundException>();
    }

    [Fact]
    public void AStayNotCheckedOut_ShouldNotBeEvaluated()
    {
        var submit = () => StayReview.Submit(FinishedStay(CheckedOutAt, checkedOut: false), Guest, new StayRatings(5), null,
            CheckedOutAt);

        submit.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(BookingErrorCodes.ReviewStayNotFinished);
    }

    [Fact]
    public void AStay_ShouldNotBeEvaluatedAfterTheWindow()
    {
        var booking = FinishedStay(CheckedOutAt);

        StayReview.Submit(booking, Guest, new StayRatings(5), null, CheckedOutAt.AddDays(StayReview.ReviewWindowDays))
            .Should().NotBeNull();
        var late = () => StayReview.Submit(booking, Guest, new StayRatings(5), null,
            CheckedOutAt.AddDays(StayReview.ReviewWindowDays).AddMinutes(1));
        late.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(BookingErrorCodes.ReviewWindowClosed);
    }

    [Theory]
    [InlineData(0, null, "overallRating")]
    [InlineData(6, null, "overallRating")]
    [InlineData(3, 0, "maintenanceRating")]
    public void RatingsOutOf1To5_ShouldBeRefused(int overall, int? maintenance, string field)
    {
        var submit = () => StayReview.Submit(FinishedStay(CheckedOutAt), Guest,
            new StayRatings(overall, Maintenance: maintenance), null, CheckedOutAt);

        var error = submit.Should().Throw<InvalidFieldException>().Which;
        error.Violation.Code.Should().Be(BookingErrorCodes.ReviewRatingOutOfRange);
        error.Field.Should().Be(field);
    }

    [Fact]
    public void ALongComment_ShouldBeRefused()
    {
        var submit = () => StayReview.Submit(FinishedStay(CheckedOutAt), Guest, new StayRatings(5),
            new string('a', StayReview.CommentMaxLength + 1), CheckedOutAt);

        submit.Should().Throw<InvalidFieldException>().Which.Violation.Code.Should().Be(BookingErrorCodes.ReviewCommentTooLong);
    }

    [Fact]
    public void Satisfaction_ShouldGroupTheRatingsByArea()
    {
        var reviews = new[]
        {
            Review(new StayRatings(5, FrontDesk: 5, Housekeeping: 4, Maintenance: 2), guestUserId: 1),
            Review(new StayRatings(4, FrontDesk: 5, Housekeeping: 5, Maintenance: 3), guestUserId: 2),
            Review(new StayRatings(3, FrontDesk: 4, Maintenance: 2), guestUserId: 3)
        };

        var report = StaySatisfactionCalculator.Summarize(HotelId, CheckedOutAt, CheckedOutAt.AddDays(1), reviews);

        report.Overall.Should().BeEquivalentTo(new { Count = 3, Average = 4d, SatisfiedPercent = 66.7, Distribution = new[] { 0, 0, 1, 1, 1 } });
        report.Areas.Select(a => (a.Area, a.Score.Count, a.Score.Average, a.Score.SatisfiedPercent)).Should().Equal(
            (SatisfactionArea.FrontDesk, 3, 4.67, 100d),
            (SatisfactionArea.Housekeeping, 2, 4.5, 100d),
            (SatisfactionArea.Maintenance, 3, 2.33, 0d));
        report.Areas.Single(a => a.Area == SatisfactionArea.Maintenance).Score.Distribution.Should().Equal(0, 2, 1, 0, 0);
    }

    [Fact]
    public void Satisfaction_WithoutReviews_ShouldListEveryAreaWithoutAverages()
    {
        var report = StaySatisfactionCalculator.Summarize(HotelId, CheckedOutAt, CheckedOutAt.AddDays(1), new List<StayReview>());

        report.Overall.Average.Should().BeNull();
        report.Areas.Should().HaveCount(3).And.OnlyContain(a => a.Score.Count == 0 && a.Score.Average == null
                                                                 && a.Score.SatisfiedPercent == null);
    }

    private static StayReview Review(StayRatings ratings, int guestUserId) =>
        StayReview.Submit(FinishedStay(CheckedOutAt, guestUserId), BookingRequester.Guest(guestUserId, "guest@example.com"),
            ratings, null, CheckedOutAt.AddHours(1));
}
