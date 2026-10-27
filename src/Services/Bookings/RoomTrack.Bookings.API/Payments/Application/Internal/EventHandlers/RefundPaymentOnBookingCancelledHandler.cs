using RoomTrack.Bookings.Domain.Model.Events;
using RoomTrack.Payments.Domain.Repositories;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.Shared.Domain.Repositories;

namespace RoomTrack.Payments.Application.Internal.EventHandlers;

/// <summary>
///     R2: when a paid booking is cancelled its payment is marked Refunded (the money goes back outside the system)
///     and stops counting as revenue. Idempotent: an already refunded payment is left as it is.
/// </summary>
public class RefundPaymentOnBookingCancelledHandler(
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IDomainEventHandler<BookingCancelledEvent>
{
    public async Task HandleAsync(BookingCancelledEvent e, CancellationToken cancellationToken)
    {
        if (!e.WasPaid) return;
        var payment = await paymentRepository.FindCompletedByBookingIdAsync(e.BookingId);
        if (payment is null || !payment.Refund(timeProvider.GetUtcNow())) return;
        await unitOfWork.CompleteAsync();
    }
}
