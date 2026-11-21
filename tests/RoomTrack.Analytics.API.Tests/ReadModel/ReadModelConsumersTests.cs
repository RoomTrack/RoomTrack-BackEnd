using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RoomTrack.Analytics.API.Infrastructure.Persistence;
using RoomTrack.Analytics.Application.Internal.Consumers;
using RoomTrack.Analytics.Domain.Model.ReadModels;
using RoomTrack.Contracts.Messaging;
using Xunit;
using static RoomTrack.Analytics.API.Tests.AnalyticsTestSupport;

namespace RoomTrack.Analytics.API.Tests.ReadModel;

/// <summary>
///     The consumers keep the read model of rooms, bookings and payments up to date from the integration events,
///     and an event older than the stored state (late redelivery) never overwrites it.
/// </summary>
public class ReadModelConsumersTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly DbContextOptions<AnalyticsDbContext> _database = NewDatabase();

    [Fact]
    public async Task RoomRegistered_ShouldAddTheRoom_OfItsHotel()
    {
        await using (var context = new AnalyticsDbContext(_database))
            await new RoomRegisteredConsumer(context).Consume(ContextFor(new RoomRegisteredIntegrationEvent(7, HotelId: 2, T0)));

        var room = await Find<RoomFact>(7);
        room.HotelId.Should().Be(2);
        room.Removed.Should().BeFalse();
        room.LastEventAt.Should().Be(T0);
    }

    [Fact]
    public async Task RoomRemoved_ShouldKeepATombstone_SoALateRegisteredEventDoesNotBringItBack()
    {
        await using (var context = new AnalyticsDbContext(_database))
        {
            await new RoomRegisteredConsumer(context).Consume(ContextFor(new RoomRegisteredIntegrationEvent(7, 2, T0)));
            await new RoomRemovedConsumer(context).Consume(ContextFor(new RoomRemovedIntegrationEvent(7, 2, T0.AddMinutes(5))));
            // Redelivered late: older than the removal
            await new RoomRegisteredConsumer(context).Consume(ContextFor(new RoomRegisteredIntegrationEvent(7, 2, T0.AddMinutes(1))));
        }

        var room = await Find<RoomFact>(7);
        room.Removed.Should().BeTrue();
        room.LastEventAt.Should().Be(T0.AddMinutes(5));
    }

    [Fact]
    public async Task RoomRemoved_BeforeItsRegistration_ShouldStillBeRemoved()
    {
        await using (var context = new AnalyticsDbContext(_database))
        {
            await new RoomRemovedConsumer(context).Consume(ContextFor(new RoomRemovedIntegrationEvent(7, 2, T0.AddMinutes(5))));
            await new RoomRegisteredConsumer(context).Consume(ContextFor(new RoomRegisteredIntegrationEvent(7, 2, T0)));
        }

        (await Find<RoomFact>(7)).Removed.Should().BeTrue();
    }

    [Fact]
    public async Task BookingStateChanged_ShouldUpsertTheBooking_WithItsLatestState()
    {
        var checkIn = new DateTime(2026, 11, 10);
        await using (var context = new AnalyticsDbContext(_database))
        {
            var consumer = new BookingStateChangedConsumer(context);
            await consumer.Consume(ContextFor(new BookingStateChangedIntegrationEvent(5, 1, 101, checkIn, checkIn.AddDays(2), "Pending", T0)));
            await consumer.Consume(ContextFor(new BookingStateChangedIntegrationEvent(5, 1, 102, checkIn, checkIn.AddDays(3), "Confirmed", T0.AddHours(1))));
        }

        var booking = await Find<BookingFact>(5);
        booking.Status.Should().Be("Confirmed");
        booking.RoomId.Should().Be(102);
        booking.CheckOutDate.Should().Be(checkIn.AddDays(3));
    }

    [Fact]
    public async Task BookingStateChanged_OlderThanTheStoredState_ShouldBeIgnored()
    {
        var checkIn = new DateTime(2026, 11, 10);
        await using (var context = new AnalyticsDbContext(_database))
        {
            var consumer = new BookingStateChangedConsumer(context);
            await consumer.Consume(ContextFor(new BookingStateChangedIntegrationEvent(5, 1, 101, checkIn, checkIn.AddDays(2), "Cancelled", T0.AddHours(2))));
            await consumer.Consume(ContextFor(new BookingStateChangedIntegrationEvent(5, 1, 101, checkIn, checkIn.AddDays(2), "Confirmed", T0.AddHours(1))));
        }

        (await Find<BookingFact>(5)).Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task PaymentStateChanged_ShouldRecordTheRefund_AfterTheCompletion()
    {
        var paidOn = new DateTime(2026, 11, 3, 10, 0, 0);
        await using (var context = new AnalyticsDbContext(_database))
        {
            var consumer = new PaymentStateChangedConsumer(context);
            await consumer.Consume(ContextFor(new PaymentStateChangedIntegrationEvent(9, 5, 1, 300m, paidOn, "Completed", T0)));
            await consumer.Consume(ContextFor(new PaymentStateChangedIntegrationEvent(9, 5, 1, 300m, paidOn, "Refunded", T0.AddDays(1))));
            // The completion redelivered after the refund does not revive it
            await consumer.Consume(ContextFor(new PaymentStateChangedIntegrationEvent(9, 5, 1, 300m, paidOn, "Completed", T0)));
        }

        var payment = await Find<PaymentFact>(9);
        payment.Status.Should().Be("Refunded");
        payment.Amount.Should().Be(300m);
        payment.BookingId.Should().Be(5);
    }

    [Fact]
    public void IsOlderThan_ShouldOnlyAcceptStrictlyNewerEvents()
    {
        var room = new RoomFact(1);
        room.Apply(1, removed: false, T0);

        room.IsOlderThan(T0.AddTicks(1)).Should().BeTrue();
        room.IsOlderThan(T0).Should().BeFalse("the same event redelivered changes nothing");
        room.IsOlderThan(T0.AddTicks(-1)).Should().BeFalse();
    }

    private async Task<T> Find<T>(int id) where T : class
    {
        await using var context = new AnalyticsDbContext(_database);
        var fact = await context.Set<T>().FindAsync(id);
        fact.Should().NotBeNull();
        return fact!;
    }
}
