using BackendAwRoomTrack.API.Bookings.Domain.Model.Events;
using BackendAwRoomTrack.API.Bookings.Domain.Repositories;
using BackendAwRoomTrack.API.Payments.Domain.Model.Events;
using BackendAwRoomTrack.API.Payments.Domain.Repositories;
using BackendAwRoomTrack.API.Shared.Application.Internal.EventHandlers;
using MassTransit;
using RoomTrack.Contracts.Messaging;

namespace RoomTrack.Bookings.API.Application.IntegrationEvents;

/// <summary>
///     Translates the domain events of Bookings and Payments into integration events for the other services
///     (Analytics). The domain events are handled inside the transaction of the change, so the integration events are
///     written to the outbox by that same transaction: they reach RabbitMQ if and only if the change commits.
/// </summary>
/// <remarks>
///     Each integration event carries the current state of the booking or payment, not the change: the consumers
///     keep a copy without calling this service back (event-carried state transfer).
/// </remarks>
public class BookingsIntegrationEventPublisher(
    IBookingRepository bookingRepository,
    IPaymentRepository paymentRepository,
    IPublishEndpoint publishEndpoint)
    : IDomainEventHandler<BookingCreatedEvent>,
        IDomainEventHandler<BookingConfirmedEvent>,
        IDomainEventHandler<BookingCancelledEvent>,
        IDomainEventHandler<BookingRescheduledEvent>,
        IDomainEventHandler<GuestCheckedInEvent>,
        IDomainEventHandler<PaymentCompletedEvent>,
        IDomainEventHandler<PaymentRefundedEvent>
{
    public Task HandleAsync(BookingCreatedEvent e, CancellationToken cancellationToken) =>
        PublishBookingStateAsync(e.BookingId, e.OccurredOn, cancellationToken);

    public Task HandleAsync(BookingConfirmedEvent e, CancellationToken cancellationToken) =>
        PublishBookingStateAsync(e.BookingId, e.OccurredOn, cancellationToken);

    public Task HandleAsync(BookingCancelledEvent e, CancellationToken cancellationToken) =>
        PublishBookingStateAsync(e.BookingId, e.OccurredOn, cancellationToken);

    public Task HandleAsync(BookingRescheduledEvent e, CancellationToken cancellationToken) =>
        PublishBookingStateAsync(e.BookingId, e.OccurredOn, cancellationToken);

    public Task HandleAsync(GuestCheckedInEvent e, CancellationToken cancellationToken) =>
        PublishBookingStateAsync(e.BookingId, e.OccurredOn, cancellationToken);

    public Task HandleAsync(PaymentCompletedEvent e, CancellationToken cancellationToken) =>
        PublishPaymentStateAsync(e.PaymentId, e.OccurredOn, cancellationToken);

    public Task HandleAsync(PaymentRefundedEvent e, CancellationToken cancellationToken) =>
        PublishPaymentStateAsync(e.PaymentId, e.OccurredOn, cancellationToken);

    private async Task PublishBookingStateAsync(int bookingId, DateTimeOffset occurredOn, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.FindByIdAsync(bookingId);
        if (booking is null) return;

        await publishEndpoint.Publish(new BookingStateChangedIntegrationEvent(
            booking.Id, booking.HotelId, booking.RoomId, booking.CheckInDate, booking.CheckOutDate,
            booking.Status.ToString(), occurredOn), cancellationToken);
    }

    private async Task PublishPaymentStateAsync(int paymentId, DateTimeOffset occurredOn, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.FindByIdAsync(paymentId);
        if (payment is null) return;
        var booking = await bookingRepository.FindByIdAsync(payment.BookingId);
        if (booking is null) return;

        await publishEndpoint.Publish(new PaymentStateChangedIntegrationEvent(
            payment.Id, payment.BookingId, booking.HotelId, payment.Amount, payment.PaymentDate,
            payment.Status.ToString(), occurredOn), cancellationToken);
    }
}
