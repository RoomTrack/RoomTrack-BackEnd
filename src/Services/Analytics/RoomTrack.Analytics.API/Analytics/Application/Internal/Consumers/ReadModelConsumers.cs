using BackendAwRoomTrack.API.Analytics.Domain.Model.ReadModels;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using MassTransit;
using RoomTrack.Contracts.Messaging;

namespace BackendAwRoomTrack.API.Analytics.Application.Internal.Consumers;

// Consumers of the integration events that feed the read model of the metrics. Each one upserts the row of its
// entity, ignoring events older than the stored state. Redeliveries are de-duplicated by the inbox of the
// MassTransit transactional outbox, which commits the inbox state and the row in one transaction.

/// <summary>Rooms registered in Accommodations.</summary>
public class RoomRegisteredConsumer(AppDbContext context) : IConsumer<RoomRegisteredIntegrationEvent>
{
    public async Task Consume(ConsumeContext<RoomRegisteredIntegrationEvent> message)
    {
        var e = message.Message;
        var room = await context.Set<RoomFact>().FindAsync(e.RoomId);
        if (room is null) context.Set<RoomFact>().Add(room = new RoomFact(e.RoomId));
        if (!room.IsOlderThan(e.OccurredOn)) return;

        room.Apply(e.HotelId, removed: false, e.OccurredOn);
        await context.SaveChangesAsync(message.CancellationToken);
    }
}

/// <summary>Rooms deleted in Accommodations.</summary>
public class RoomRemovedConsumer(AppDbContext context) : IConsumer<RoomRemovedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<RoomRemovedIntegrationEvent> message)
    {
        var e = message.Message;
        var room = await context.Set<RoomFact>().FindAsync(e.RoomId);
        if (room is null) context.Set<RoomFact>().Add(room = new RoomFact(e.RoomId));
        if (!room.IsOlderThan(e.OccurredOn)) return;

        room.Apply(e.HotelId, removed: true, e.OccurredOn);
        await context.SaveChangesAsync(message.CancellationToken);
    }
}

/// <summary>Bookings created or changed in Bookings.</summary>
public class BookingStateChangedConsumer(AppDbContext context) : IConsumer<BookingStateChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<BookingStateChangedIntegrationEvent> message)
    {
        var e = message.Message;
        var booking = await context.Set<BookingFact>().FindAsync(e.BookingId);
        if (booking is null) context.Set<BookingFact>().Add(booking = new BookingFact(e.BookingId));
        if (!booking.IsOlderThan(e.OccurredOn)) return;

        booking.Apply(e.HotelId, e.RoomId, e.CheckInDate, e.CheckOutDate, e.Status, e.OccurredOn);
        await context.SaveChangesAsync(message.CancellationToken);
    }
}

/// <summary>Payments completed or refunded in Bookings.</summary>
public class PaymentStateChangedConsumer(AppDbContext context) : IConsumer<PaymentStateChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<PaymentStateChangedIntegrationEvent> message)
    {
        var e = message.Message;
        var payment = await context.Set<PaymentFact>().FindAsync(e.PaymentId);
        if (payment is null) context.Set<PaymentFact>().Add(payment = new PaymentFact(e.PaymentId));
        if (!payment.IsOlderThan(e.OccurredOn)) return;

        payment.Apply(e.BookingId, e.HotelId, e.Amount, e.PaymentDate, e.Status, e.OccurredOn);
        await context.SaveChangesAsync(message.CancellationToken);
    }
}
