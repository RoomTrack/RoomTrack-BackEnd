using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RoomTrack.Analytics.API.Infrastructure.Persistence;
using RoomTrack.Analytics.Application.Internal.Consumers;
using RoomTrack.Analytics.Application.Internal.QueryServices;
using RoomTrack.Analytics.Domain.Model.Queries;
using RoomTrack.Analytics.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Contracts.Messaging;
using Xunit;
using static RoomTrack.Analytics.API.Tests.AnalyticsTestSupport;

namespace RoomTrack.Analytics.API.Tests.Metrics;

/// <summary>
///     Monthly metrics of a hotel (or of the whole chain) computed from the read model: revenue of the completed
///     payments, bookings and cancellations by check-in date, and occupancy of the rooms that still exist.
/// </summary>
public class MonthlyPerformanceTests
{
    private const int Hotel = 1;
    private const int OtherHotel = 2;

    private static readonly DateTime StartOfMonth = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    private static readonly DateTime LastDayOfMonth = StartOfMonth.AddMonths(1).AddDays(-1);
    private static readonly DateTimeOffset At = DateTimeOffset.UtcNow;

    private readonly DbContextOptions<AnalyticsDbContext> _database = NewDatabase();
    private int _nextId = 1;

    [Fact]
    public async Task Revenue_ShouldAddTheCompletedPaymentsOfTheMonth_OfTheHotelOnly()
    {
        await Payment(Hotel, 200m, StartOfMonth.AddDays(1), "Completed");
        await Payment(Hotel, 150.50m, StartOfMonth.AddDays(2), "Completed");
        await Payment(Hotel, 999m, StartOfMonth.AddDays(2), "Refunded");
        await Payment(Hotel, 500m, StartOfMonth.AddDays(-1), "Completed"); // last month
        await Payment(OtherHotel, 80m, StartOfMonth.AddDays(1), "Completed");

        var metrics = await Metrics(Hotel);

        metrics.TotalRevenue.Should().Be(350.50m);
    }

    [Fact]
    public async Task Revenue_ShouldIncludeAPaymentMadeDuringTheLastDayOfTheMonth()
    {
        await Payment(Hotel, 120m, LastDayOfMonth.AddHours(18), "Completed");
        await Payment(Hotel, 70m, StartOfMonth.AddMonths(1), "Completed"); // next month

        (await Metrics(Hotel)).TotalRevenue.Should().Be(120m);
    }

    [Fact]
    public async Task Bookings_ShouldCountThoseCheckingInThisMonth_AndTheirCancellations()
    {
        await Booking(Hotel, StartOfMonth.AddDays(3), "Confirmed");
        await Booking(Hotel, StartOfMonth.AddDays(4), "Cancelled");
        await Booking(Hotel, LastDayOfMonth, "Pending");
        await Booking(Hotel, StartOfMonth.AddMonths(1), "Confirmed"); // next month
        await Booking(OtherHotel, StartOfMonth.AddDays(3), "Cancelled");

        var metrics = await Metrics(Hotel);

        metrics.TotalBookings.Should().Be(3);
        metrics.CancelledBookings.Should().Be(1);
    }

    [Fact]
    public async Task Occupancy_ShouldBeTheActiveBookingsOverTheRoomsThatStillExist()
    {
        await Room(Hotel);
        await Room(Hotel);
        await Room(Hotel);
        var removed = await Room(Hotel);
        await RemoveRoom(removed, Hotel);
        await Booking(Hotel, StartOfMonth.AddDays(1), "Confirmed");
        await Booking(Hotel, StartOfMonth.AddDays(2), "CheckedIn");
        await Booking(Hotel, StartOfMonth.AddDays(2), "Cancelled");
        await Booking(Hotel, StartOfMonth.AddDays(2), "Pending");

        var metrics = await Metrics(Hotel);

        // 2 active bookings / 3 rooms
        metrics.OccupancyRate.Should().Be(66.67);
    }

    [Fact]
    public async Task Occupancy_WithoutRooms_ShouldBeZero()
    {
        await Booking(Hotel, StartOfMonth.AddDays(1), "Confirmed");

        (await Metrics(Hotel)).OccupancyRate.Should().Be(0);
    }

    [Fact]
    public async Task TheWholeChain_ShouldAddEveryHotel()
    {
        await Payment(Hotel, 100m, StartOfMonth.AddDays(1), "Completed");
        await Payment(OtherHotel, 50m, StartOfMonth.AddDays(1), "Completed");
        await Booking(Hotel, StartOfMonth.AddDays(1), "Confirmed");
        await Booking(OtherHotel, StartOfMonth.AddDays(1), "Confirmed");

        await using var context = new AnalyticsDbContext(_database);
        var metrics = await new AnalyticsQueryService(new AnalyticsRepository(context))
            .Handle(new GetMonthlyPerformanceQuery(HotelId: null, WholeChain: true));

        metrics.TotalRevenue.Should().Be(150m);
        metrics.TotalBookings.Should().Be(2);
    }

    [Fact]
    public async Task AnAdminWithoutHotel_ShouldGetEmptyMetrics_InsteadOfTheWholeChain()
    {
        await Payment(Hotel, 100m, StartOfMonth.AddDays(1), "Completed");
        await Booking(Hotel, StartOfMonth.AddDays(1), "Confirmed");

        await using var context = new AnalyticsDbContext(_database);
        var metrics = await new AnalyticsQueryService(new AnalyticsRepository(context))
            .Handle(new GetMonthlyPerformanceQuery(HotelId: null, WholeChain: false));

        metrics.TotalRevenue.Should().Be(0);
        metrics.TotalBookings.Should().Be(0);
        metrics.OccupancyRate.Should().Be(0);
    }

    private async Task<Domain.Model.Aggregates.PerformanceMetrics> Metrics(int hotelId)
    {
        await using var context = new AnalyticsDbContext(_database);
        return await new AnalyticsQueryService(new AnalyticsRepository(context))
            .Handle(new GetMonthlyPerformanceQuery(hotelId, WholeChain: false));
    }

    // The read model is filled through its consumers, as RabbitMQ would.

    private async Task<int> Room(int hotelId)
    {
        var id = _nextId++;
        await using var context = new AnalyticsDbContext(_database);
        await new RoomRegisteredConsumer(context).Consume(ContextFor(new RoomRegisteredIntegrationEvent(id, hotelId, At)));
        return id;
    }

    private async Task RemoveRoom(int roomId, int hotelId)
    {
        await using var context = new AnalyticsDbContext(_database);
        await new RoomRemovedConsumer(context).Consume(ContextFor(new RoomRemovedIntegrationEvent(roomId, hotelId, At.AddSeconds(1))));
    }

    private async Task Booking(int hotelId, DateTime checkIn, string status)
    {
        await using var context = new AnalyticsDbContext(_database);
        await new BookingStateChangedConsumer(context).Consume(ContextFor(
            new BookingStateChangedIntegrationEvent(_nextId++, hotelId, RoomId: 1, checkIn, checkIn.AddDays(2), status, At)));
    }

    private async Task Payment(int hotelId, decimal amount, DateTime paidOn, string status)
    {
        await using var context = new AnalyticsDbContext(_database);
        await new PaymentStateChangedConsumer(context).Consume(ContextFor(
            new PaymentStateChangedIntegrationEvent(_nextId++, BookingId: 1, hotelId, amount, paidOn, status, At)));
    }
}
