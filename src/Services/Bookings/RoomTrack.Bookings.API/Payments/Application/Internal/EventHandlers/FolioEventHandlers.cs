using RoomTrack.Bookings.Domain.Model.Events;
using RoomTrack.Payments.Application.Internal.CommandServices;
using RoomTrack.Payments.Application.OutboundServices;
using RoomTrack.Payments.Domain.Model.Events;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Payments.Domain.Repositories;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.Shared.Domain.Repositories;

namespace RoomTrack.Payments.Application.Internal.EventHandlers;

/// <summary>
///     US-17: every completed payment, of the booking or of the folio, gets its receipt automatically (in the same
///     transaction), e-mailed to the guest.
/// </summary>
public class IssueReceiptOnPaymentHandler(ReceiptIssuer receiptIssuer, IUnitOfWork unitOfWork) :
    IDomainEventHandler<PaymentCompletedEvent>,
    IDomainEventHandler<FolioPaymentRecordedEvent>
{
    public async Task HandleAsync(PaymentCompletedEvent e, CancellationToken cancellationToken)
    {
        if (await receiptIssuer.IssueAsync(ReceiptSource.BookingPayment, e.PaymentId, e.BookingId, e.Amount, e.Method) is not null)
            await unitOfWork.CompleteAsync();
    }

    public async Task HandleAsync(FolioPaymentRecordedEvent e, CancellationToken cancellationToken)
    {
        if (await receiptIssuer.IssueAsync(ReceiptSource.FolioPayment, e.FolioPaymentId, e.BookingId, e.Amount, e.Method) is not null)
            await unitOfWork.CompleteAsync();
    }
}

/// <summary>
///     The check-out closes the folio of the stay (US-16): a held deposit is released (its refund starts) and the guest
///     receives the statement of the stay with its receipts (US-14). The check-out already verified that nothing was
///     unpaid (<c>IStayFolioGate</c>).
/// </summary>
public class CloseFolioOnCheckOutHandler(
    IGuestFolioRepository folioRepository,
    IReceiptRepository receiptRepository,
    IReceiptNotificationService notificationService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IDomainEventHandler<GuestCheckedOutEvent>
{
    public async Task HandleAsync(GuestCheckedOutEvent e, CancellationToken cancellationToken)
    {
        var folio = await folioRepository.FindByBookingIdAsync(e.BookingId);
        if (folio is null) return;
        folio.CloseForCheckOut(timeProvider.GetUtcNow());
        await notificationService.SendFolioStatementAsync(folio, await receiptRepository.ListByBookingIdAsync(e.BookingId));
        await unitOfWork.CompleteAsync();
    }
}
