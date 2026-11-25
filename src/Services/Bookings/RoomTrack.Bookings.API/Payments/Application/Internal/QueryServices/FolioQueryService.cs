using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.Commands;
using RoomTrack.Payments.Domain.Model.Queries;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Payments.Domain.Repositories;
using RoomTrack.Payments.Domain.Services;

namespace RoomTrack.Payments.Application.Internal.QueryServices;

public class FolioQueryService(
    IGuestFolioRepository folioRepository,
    IReceiptRepository receiptRepository,
    IPaymentRepository paymentRepository,
    IBookingsContextFacade bookingsContextFacade,
    TimeProvider timeProvider) : IFolioQueryService
{
    public async Task<GuestFolio?> Handle(GetFolioByBookingIdQuery query)
    {
        var booking = await VisibleBookingAsync(query.BookingId, query.Actor);
        if (booking is null) return null;
        // A stay without charges, deposit nor invoice data has an empty folio (not stored until something happens).
        return await folioRepository.FindByBookingIdAsync(booking.BookingId)
               ?? GuestFolio.Open(booking.BookingId, booking.HotelId, booking.Code, booking.GuestName, booking.GuestEmail,
                   timeProvider.GetUtcNow());
    }

    public async Task<IReadOnlyList<Receipt>?> Handle(GetReceiptsByBookingIdQuery query)
    {
        var booking = await VisibleBookingAsync(query.BookingId, query.Actor);
        return booking is null ? null : await receiptRepository.ListByBookingIdAsync(booking.BookingId);
    }

    public async Task<Receipt?> Handle(GetReceiptByIdQuery query)
    {
        var receipt = await receiptRepository.FindByIdAsync(query.ReceiptId);
        if (receipt is null) return null;
        return await VisibleBookingAsync(receipt.BookingId, query.Actor) is null ? null : receipt;
    }

    public async Task<IReadOnlyList<PaymentHistoryEntry>> Handle(GetPaymentHistoryQuery query)
    {
        var entries = new List<PaymentHistoryEntry>();

        var bookingIds = await bookingsContextFacade.FetchBookingIdsOfHotelAsync(query.HotelId);
        foreach (var payment in await paymentRepository.ListSettledOfBookingsAsync(bookingIds, query.From, query.To))
        {
            var paidAt = new DateTimeOffset(DateTime.SpecifyKind(payment.PaymentDate, DateTimeKind.Utc));
            if (paidAt >= query.From && paidAt <= query.To)
                entries.Add(new PaymentHistoryEntry(paidAt, "BookingPayment", payment.BookingId, payment.Amount,
                    payment.Method.ToString(), payment.OperationNumber ?? payment.TransactionId, null));
            if (payment.RefundedAt is { } refundedAt && refundedAt >= query.From && refundedAt <= query.To)
                entries.Add(new PaymentHistoryEntry(refundedAt, "Refund", payment.BookingId, -payment.Amount,
                    payment.Method.ToString(), payment.TransactionId, null));
        }

        foreach (var folio in await folioRepository.ListWithActivityAsync(query.HotelId, query.From, query.To))
        {
            entries.AddRange(folio.Payments.Where(payment => payment.PaidAt >= query.From && payment.PaidAt <= query.To)
                .Select(payment => new PaymentHistoryEntry(payment.PaidAt, "FolioPayment", folio.BookingId, payment.Amount,
                    payment.Method.ToString(), payment.OperationNumber, null)));
            if (folio.DepositHeldAt is { } heldAt && heldAt >= query.From && heldAt <= query.To)
                entries.Add(new PaymentHistoryEntry(heldAt, "DepositHeld", folio.BookingId, folio.DepositAmount!.Value,
                    folio.DepositMethod?.ToString(), folio.DepositOperationNumber, null));
            if (folio.DepositStatus == DepositStatus.Released && folio.DepositReleasedAt is { } releasedAt
                && releasedAt >= query.From && releasedAt <= query.To)
                entries.Add(new PaymentHistoryEntry(releasedAt, "DepositReleased", folio.BookingId, -folio.DepositAmount!.Value,
                    folio.DepositMethod?.ToString(), folio.DepositOperationNumber, null));
        }

        entries.AddRange((await receiptRepository.ListByHotelAsync(query.HotelId, query.From, query.To))
            .Select(receipt => new PaymentHistoryEntry(receipt.IssuedAt, "Receipt", receipt.BookingId, receipt.Total,
                receipt.Method.ToString(), receipt.FullNumber, receipt.Id)));

        return entries.OrderByDescending(entry => entry.OccurredAt).Take(Math.Clamp(query.Limit, 1, 2000)).ToList();
    }

    private async Task<BookingSnapshot?> VisibleBookingAsync(int bookingId, FolioActor actor)
    {
        var booking = await bookingsContextFacade.FetchBookingAsync(bookingId, actor.IsGuest ? actor.UserId : null);
        return booking is null || (!actor.IsGuest && !actor.OperatesHotel(booking.HotelId)) ? null : booking;
    }
}
