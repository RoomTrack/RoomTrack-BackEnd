using System;
using System.Globalization;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using RoomTrack.Analytics.API.Infrastructure.Persistence;
using RoomTrack.Analytics.Application.Internal.Consumers;
using RoomTrack.Analytics.Application.Internal.QueryServices;
using RoomTrack.Analytics.Domain.Model.Aggregates;
using RoomTrack.Analytics.Domain.Model.Queries;
using RoomTrack.Analytics.Infrastructure.Persistence.EFC.Repositories;
using RoomTrack.Contracts.Messaging;
using static RoomTrack.Analytics.API.Tests.AnalyticsTestSupport;

namespace RoomTrack.Analytics.API.Tests.Features.Steps;

/// <summary>
///     Steps of the dashboard (US-18): the integration events reach the real read model consumers, and the metrics are
///     read through the real query service, on an in-memory database.
/// </summary>
[Binding]
public sealed class DashboardSteps
{
    private static readonly DateTime StartOfMonth = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    private static readonly DateTimeOffset At = DateTimeOffset.UtcNow;

    private readonly DbContextOptions<AnalyticsDbContext> _database = NewDatabase();
    private int _nextId = 1;
    private int _lastRoomId;
    private PerformanceMetrics? _metrics;

    [Given("que el hotel {int} tiene {int} habitaciones registradas")]
    public async Task GivenRooms(int hotelId, int rooms)
    {
        for (var i = 0; i < rooms; i++)
        {
            _lastRoomId = _nextId++;
            await using var context = new AnalyticsDbContext(_database);
            await new RoomRegisteredConsumer(context).Consume(ContextFor(new RoomRegisteredIntegrationEvent(_lastRoomId, hotelId, At)));
        }
    }

    [Given("que en el hotel {int} se completaron pagos de {decimal} y {decimal} este mes")]
    public async Task GivenCompletedPayments(int hotelId, decimal first, decimal second)
    {
        await Payment(hotelId, first, "Completed");
        await Payment(hotelId, second, "Completed");
    }

    [Given("se reembolsó un pago de {decimal}")]
    public Task GivenARefund(decimal amount) => Payment(1, amount, "Refunded");

    [Given("el hotel {int} cobró {decimal}")]
    public Task GivenAnotherHotelCharged(int hotelId, decimal amount) => Payment(hotelId, amount, "Completed");

    [Given("que el hotel {int} tiene este mes {int} reservas confirmadas, {int} en estancia y {int} cancelada")]
    public async Task GivenBookings(int hotelId, int confirmed, int staying, int cancelled)
    {
        for (var i = 0; i < confirmed; i++) await Booking(hotelId, "Confirmed");
        for (var i = 0; i < staying; i++) await Booking(hotelId, "CheckedIn");
        for (var i = 0; i < cancelled; i++) await Booking(hotelId, "Cancelled");
    }

    [Given("se retira una habitación del hotel {int}")]
    public async Task GivenARoomIsRemoved(int hotelId)
    {
        await using var context = new AnalyticsDbContext(_database);
        await new RoomRemovedConsumer(context).Consume(ContextFor(new RoomRemovedIntegrationEvent(_lastRoomId, hotelId, At.AddSeconds(1))));
    }

    [When("abro el panel del hotel {int}")]
    public async Task WhenIOpenTheDashboard(int hotelId) => _metrics = await Metrics(new GetMonthlyPerformanceQuery(hotelId, WholeChain: false));

    [When("abro el panel de toda la cadena")]
    public async Task WhenIOpenTheChainDashboard() =>
        _metrics = await Metrics(new GetMonthlyPerformanceQuery(HotelId: null, WholeChain: true));

    [Then("veo ingresos por {decimal}")]
    public void ThenTheRevenueIs(decimal revenue) => _metrics!.TotalRevenue.Should().Be(revenue);

    [Then("veo {int} reservas, {int} cancelada y una ocupación de {int} %")]
    public void ThenTheBookingsAre(int bookings, int cancelled, int occupancy)
    {
        _metrics!.TotalBookings.Should().Be(bookings);
        _metrics.CancelledBookings.Should().Be(cancelled);
        _metrics.OccupancyRate.Should().Be(occupancy);
    }

    [Then("la ocupación es de {word} %")]
    public void ThenTheOccupancyIs(string occupancy) =>
        _metrics!.OccupancyRate.Should().Be(double.Parse(occupancy, CultureInfo.InvariantCulture));

    private async Task<PerformanceMetrics> Metrics(GetMonthlyPerformanceQuery query)
    {
        await using var context = new AnalyticsDbContext(_database);
        return await new AnalyticsQueryService(new AnalyticsRepository(context)).Handle(query);
    }

    private async Task Payment(int hotelId, decimal amount, string status)
    {
        await using var context = new AnalyticsDbContext(_database);
        await new PaymentStateChangedConsumer(context).Consume(ContextFor(
            new PaymentStateChangedIntegrationEvent(_nextId++, BookingId: 1, hotelId, amount, StartOfMonth.AddHours(12), status, At)));
    }

    private async Task Booking(int hotelId, string status)
    {
        await using var context = new AnalyticsDbContext(_database);
        var checkIn = StartOfMonth.AddDays(1);
        await new BookingStateChangedConsumer(context).Consume(ContextFor(
            new BookingStateChangedIntegrationEvent(_nextId++, hotelId, RoomId: 1, checkIn, checkIn.AddDays(2), status, At)));
    }
}
