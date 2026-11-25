using RoomTrack.Bookings.Interfaces.ACL;
using RoomTrack.Payments.Application.OutboundServices;
using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Payments.Domain.Repositories;

namespace RoomTrack.Payments.Application.Internal.CommandServices;

/// <summary>
///     Issues the receipt of a completed payment (US-17) and e-mails it to the guest, inside the transaction of the
///     payment: a boleta, or a factura when the guest gave invoice data. Idempotent per payment.
/// </summary>
public class ReceiptIssuer(
    IReceiptRepository receiptRepository,
    IGuestFolioRepository folioRepository,
    IBookingsContextFacade bookingsContextFacade,
    IReceiptNotificationService notificationService,
    TimeProvider timeProvider)
{
    public async Task<Receipt?> IssueAsync(ReceiptSource source, int sourceId, int bookingId, decimal amount, PaymentMethod method)
    {
        if (await receiptRepository.ExistsForSourceAsync(source, sourceId)) return null;
        var booking = await bookingsContextFacade.FetchBookingAsync(bookingId);
        if (booking is null) return null;

        var invoiceData = (await folioRepository.FindByBookingIdAsync(bookingId))?.InvoiceData;
        var concept = source == ReceiptSource.BookingPayment
            ? $"Hospedaje - reserva {booking.Code} ({booking.Nights} {(booking.Nights == 1 ? "noche" : "noches")})"
            : $"Consumos adicionales - reserva {booking.Code}";
        var series = Receipt.SeriesOf(invoiceData is null ? ReceiptType.Boleta : ReceiptType.Factura);
        var number = await receiptRepository.NextNumberAsync(booking.HotelId, series);
        var now = timeProvider.GetUtcNow();

        var receipt = Receipt.Issue(booking.HotelId, booking.BookingId, booking.Code, source, sourceId, amount, method, concept,
            string.IsNullOrWhiteSpace(booking.GuestName) ? booking.GuestEmail : booking.GuestName, booking.GuestEmail, invoiceData,
            number, now);
        await receiptRepository.AddAsync(receipt);
        await notificationService.SendReceiptAsync(receipt);
        receipt.RecordSent(now);
        return receipt;
    }
}
