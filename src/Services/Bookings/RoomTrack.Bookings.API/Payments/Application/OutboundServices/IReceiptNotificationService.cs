using RoomTrack.Payments.Domain.Model.Aggregates;

namespace RoomTrack.Payments.Application.OutboundServices;

/// <summary>E-mails of the receipts and of the folio (enlisted in the outbox of the current transaction).</summary>
public interface IReceiptNotificationService
{
    /// <summary>The receipt of a payment (on issue, and when re-sent from the panel).</summary>
    Task SendReceiptAsync(Receipt receipt);

    /// <summary>The statement of the stay at check-out: charges, payments, deposit and receipts (US-14).</summary>
    Task SendFolioStatementAsync(GuestFolio folio, IReadOnlyList<Receipt> receipts);
}
