using System.Globalization;
using System.Text;
using RoomTrack.Payments.Application.OutboundServices;
using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Shared.Infrastructure.Configuration;
using RoomTrack.Shared.Infrastructure.Email.Templates;
using Microsoft.Extensions.Options;

namespace RoomTrack.Payments.Infrastructure.Notifications;

/// <summary>Receipt and folio e-mails to the guest (neutral Spanish).</summary>
public class ReceiptEmailNotificationService(IEmailSender emailSender, IOptions<ApplicationUrlsSettings> urls)
    : IReceiptNotificationService
{
    private static readonly CultureInfo Peru = CultureInfo.GetCultureInfo("es-PE");

    public Task SendReceiptAsync(Receipt receipt)
    {
        var email = EmailLayout.Create()
            .Greeting("Hola:")
            .Paragraph($"Te enviamos tu {(receipt.Type == ReceiptType.Factura ? "factura" : "boleta")} electrónica {receipt.FullNumber} de la reserva {receipt.BookingCode}.")
            .Paragraph($"Concepto: {receipt.Concept}. Pagado con {receipt.Method}.")
            .Paragraph($"Op. gravada {Money(receipt.Subtotal)} · IGV (18 %) {Money(receipt.Igv)} · Total {Money(receipt.Total)}.");
        if (receipt.Type == ReceiptType.Factura)
            email.Paragraph($"Emitida a {receipt.CustomerName}, RUC {receipt.CustomerDocument}{(receipt.CustomerAddress is { } address ? $", {address}" : string.Empty)}.");
        return emailSender.SendAsync(email
            .Action("Ver mis comprobantes", urls.Value.WebLink("bookings"))
            .Footnote($"Emitido el {receipt.IssuedAt.ToString("d 'de' MMMM 'de' yyyy, HH:mm", Peru)} (UTC).")
            .To(receipt.CustomerEmail, $"Tu comprobante {receipt.FullNumber}"));
    }

    public Task SendFolioStatementAsync(GuestFolio folio, IReadOnlyList<Receipt> receipts)
    {
        var email = EmailLayout.Create()
            .Greeting($"Hola, {folio.GuestName.Split(' ')[0]}:")
            .Paragraph($"Este es el estado de cuenta de tu estancia (reserva {folio.BookingCode}).");
        if (folio.Charges.Count > 0)
        {
            var charges = new StringBuilder("Consumos: ");
            charges.AppendJoin("; ", folio.Charges.Select(charge => $"{charge.Description} {Money(charge.Amount)}"));
            email.Paragraph(charges.Append($". Total {Money(folio.ChargesTotal)}, pagado {Money(folio.PaidTotal)}.").ToString());
        }
        if (folio.DepositStatus == DepositStatus.Released)
            email.Paragraph($"Tu depósito de garantía de {Money(folio.DepositAmount!.Value)} fue liberado: la devolución por {folio.DepositMethod} ya está en proceso.");
        if (receipts.Count > 0)
            email.Paragraph("Comprobantes emitidos: " + string.Join(", ", receipts.Select(receipt => $"{receipt.FullNumber} ({Money(receipt.Total)})")) + ".");
        return emailSender.SendAsync(email
            .Action("Ver mis reservas", urls.Value.WebLink("bookings"))
            .To(folio.GuestEmail, $"Estado de cuenta de tu reserva {folio.BookingCode}"));
    }

    private static string Money(decimal amount) => amount.ToString("'S/ '#,##0.00", Peru);
}
