using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RoomTrack.Bookings.API.Infrastructure.Persistence;
using RoomTrack.Bookings.Application.ACL;
using RoomTrack.Bookings.Application.Internal.Configuration;
using RoomTrack.Bookings.Domain.Model.Aggregates;
using RoomTrack.Bookings.Domain.Model.ValueObjects;
using RoomTrack.Bookings.Infrastructure.Persistence.EFC.Repositories;
using Xunit;
using static RoomTrack.Tests.Bookings.BookingTestData;

namespace RoomTrack.Tests.Bookings.Application;

/// <summary>The front desk of a hotel today, as the Bookings service reports it to the administrator's panel (US-18).</summary>
public class FrontDeskSummaryTests : IDisposable
{
    /// <summary>12/01/2030 at 15:00 in Lima: the arrival day of <see cref="BookingTestData.Stay"/>.</summary>
    private static readonly DateTimeOffset ArrivalDayAfternoon = new(2030, 1, 12, 20, 0, 0, TimeSpan.Zero);

    private readonly BookingsDbContext _context = new(new DbContextOptionsBuilder<BookingsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task TheSummary_ShouldCountArrivalsGuestsInHouseDeparturesAndTheLatestReviews()
    {
        // Arrival: paid, its stay starts today.
        var arrival = PlaceForGuest(roomId: 101);
        arrival.Confirm(Now);
        // In house: checked in today, leaves in two days.
        var inHouse = FinishedStay(ArrivalDayAfternoon.AddDays(2), guestUserId: 43, roomId: 102, checkedOut: false);
        // Departure: checked in two days ago, its stay ends today.
        var departure = Booking.Place(BookingRequester.Guest(44, "carla@example.com"), Room(103), Stay(Today, daysFromToday: 0),
            Contact(), 44, guestProfileId: null, Today, Now, PaymentHold);
        departure.Confirm(Now);
        departure.CheckIn(Today, Now);
        // Not at the front desk: unpaid, and an evaluated stay that already ended.
        var unpaid = PlaceForGuest(roomId: 104);
        var reviewed = FinishedStay(ArrivalDayAfternoon.AddDays(-1), guestUserId: 45, roomId: 105);
        var otherReviewed = FinishedStay(ArrivalDayAfternoon.AddDays(-1), guestUserId: 46, roomId: 106);
        _context.AddRange(arrival, inHouse, departure, unpaid, reviewed, otherReviewed);
        await _context.SaveChangesAsync();
        _context.AddRange(
            StayReview.Submit(reviewed, BookingRequester.Guest(45, "a@example.com"), new StayRatings(4), "Bien", ArrivalDayAfternoon.AddHours(-3)),
            StayReview.Submit(otherReviewed, BookingRequester.Guest(46, "b@example.com"), new StayRatings(5), "Excelente", ArrivalDayAfternoon.AddHours(-1)));
        await _context.SaveChangesAsync();

        var summary = await Facade().FetchFrontDeskSummaryAsync(HotelId);

        (summary.ArrivalsToday, summary.InHouse, summary.DeparturesToday).Should().Be((1, 2, 1));
        (summary.ReviewsLast30Days, summary.AverageRatingLast30Days).Should().Be((2, 4.5));
        summary.LatestReviews.Should().HaveCount(2);
        summary.LatestReviews[0].Comment.Should().Be("Excelente", "the newest first");
    }

    [Fact]
    public async Task AHotelWithoutActivity_ShouldHaveAnEmptySummary()
    {
        var summary = await Facade().FetchFrontDeskSummaryAsync(HotelId);

        summary.Should().BeEquivalentTo(new { ArrivalsToday = 0, InHouse = 0, DeparturesToday = 0, ReviewsLast30Days = 0 });
        summary.AverageRatingLast30Days.Should().BeNull();
        summary.LatestReviews.Should().BeEmpty();
    }

    private RoomReservationsFacade Facade() =>
        new(new BookingRepository(_context), bookingQueryService: null!, new StayReviewRepository(_context),
            new HotelCalendar(Options.Create(new BookingPolicySettings()), new FixedClock(ArrivalDayAfternoon)));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
