using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.Commands;
using RoomTrack.Payments.Domain.Model.Queries;

namespace RoomTrack.Payments.Domain.Services;

/// <summary>Use cases of the folio of a stay (US-15/16/17). Every booking not visible to the actor is a 404.</summary>
public interface IFolioCommandService
{
    Task<GuestFolio> Handle(PostFolioChargeCommand command);

    Task<GuestFolio> Handle(HoldDepositCommand command);

    /// <returns>The folio and the receipts issued for the payment (one per part).</returns>
    Task<(GuestFolio Folio, IReadOnlyList<Receipt> Receipts)> Handle(PayFolioCommand command);

    Task<GuestFolio> Handle(RequestInvoiceCommand command);

    /// <returns>The receipt, or null when it does not exist (or is not visible to the actor).</returns>
    Task<Receipt?> Handle(ResendReceiptCommand command);
}

/// <summary>One line of the payment history of a hotel (US-17).</summary>
/// <param name="OccurredAt">When.</param>
/// <param name="Type">BookingPayment, Refund, FolioPayment, DepositHeld, DepositReleased or Receipt.</param>
/// <param name="BookingId">The booking.</param>
/// <param name="Amount">Money in (positive) or out (negative refunds and released deposits).</param>
/// <param name="Method">How it was paid.</param>
/// <param name="Reference">Operation number, transaction or receipt number.</param>
/// <param name="ReceiptId">The receipt (Receipt lines).</param>
public record PaymentHistoryEntry(DateTimeOffset OccurredAt, string Type, int BookingId, decimal Amount, string? Method,
    string? Reference, int? ReceiptId);

public interface IFolioQueryService
{
    /// <returns>The folio (empty if none was opened yet), or null when the booking is not visible to the actor.</returns>
    Task<GuestFolio?> Handle(GetFolioByBookingIdQuery query);

    /// <returns>The receipts, or null when the booking is not visible to the actor.</returns>
    Task<IReadOnlyList<Receipt>?> Handle(GetReceiptsByBookingIdQuery query);

    Task<Receipt?> Handle(GetReceiptByIdQuery query);

    Task<IReadOnlyList<PaymentHistoryEntry>> Handle(GetPaymentHistoryQuery query);
}
