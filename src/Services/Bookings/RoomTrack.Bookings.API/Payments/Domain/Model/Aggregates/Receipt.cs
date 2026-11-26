using RoomTrack.Payments.Domain.Model.Exceptions;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Payments.Domain.Model.Aggregates;

/// <summary>
///     The electronic receipt of a payment (US-17): a boleta for the guest, or a factura for the company whose invoice
///     data the guest gave before paying. It is issued automatically when a payment completes, sent to the guest's
///     e-mail, and can be re-sent from the panel. Amounts include IGV (18 %).
/// </summary>
public class Receipt
{
    public const decimal IgvRate = 0.18m;
    public const string Currency = "PEN";
    public const int MaxConceptLength = 200;

    /// <summary>EF Core constructor.</summary>
    protected Receipt()
    {
        Series = string.Empty;
        CustomerName = string.Empty;
        CustomerEmail = string.Empty;
        Concept = string.Empty;
        BookingCode = string.Empty;
    }

    /// <summary>
    ///     Issues the receipt of a completed payment. With <paramref name="invoiceData"/> it is a factura (series F001)
    ///     to that company; otherwise a boleta (series B001) to the guest.
    /// </summary>
    /// <param name="hotelId">Hotel that issues it.</param>
    /// <param name="bookingId">The booking paid.</param>
    /// <param name="bookingCode">Its code.</param>
    /// <param name="source">The booking payment or a payment of the folio.</param>
    /// <param name="sourceId">The id of that payment (one receipt per payment).</param>
    /// <param name="total">Amount paid, IGV included.</param>
    /// <param name="method">How it was paid.</param>
    /// <param name="concept">What it documents.</param>
    /// <param name="guestName">The guest (customer of a boleta).</param>
    /// <param name="guestEmail">Where it is sent.</param>
    /// <param name="invoiceData">The company of a factura, or null for a boleta.</param>
    /// <param name="number">The next number of the series in the hotel.</param>
    /// <param name="now">The current instant.</param>
    public static Receipt Issue(int hotelId, int bookingId, string bookingCode, ReceiptSource source, int sourceId,
        decimal total, PaymentMethod method, string concept, string guestName, string guestEmail, InvoiceData? invoiceData,
        int number, DateTimeOffset now)
    {
        if (total <= 0) throw new DomainValidationException(PaymentErrorCodes.InternalInvariant, "A receipt must have a positive total.");
        if (number <= 0) throw new DomainValidationException(PaymentErrorCodes.InternalInvariant, "A receipt number is positive.");
        var type = invoiceData is null ? ReceiptType.Boleta : ReceiptType.Factura;
        var subtotal = decimal.Round(total / (1 + IgvRate), 2, MidpointRounding.AwayFromZero);
        return new Receipt
        {
            HotelId = hotelId,
            BookingId = bookingId,
            BookingCode = bookingCode,
            Type = type,
            Series = SeriesOf(type),
            Number = number,
            Source = source,
            SourceId = sourceId,
            Total = total,
            Subtotal = subtotal,
            Igv = total - subtotal,
            Method = method,
            Concept = concept.Length > MaxConceptLength ? concept[..MaxConceptLength] : concept,
            CustomerName = invoiceData?.BusinessName ?? guestName,
            CustomerDocument = invoiceData?.Ruc,
            CustomerAddress = invoiceData?.Address,
            CustomerEmail = guestEmail,
            IssuedAt = now
        };
    }

    /// <summary>The series of each receipt type.</summary>
    public static string SeriesOf(ReceiptType type) => type == ReceiptType.Factura ? "F001" : "B001";

    public int Id { get; private set; }
    public int HotelId { get; private set; }
    public int BookingId { get; private set; }
    public string BookingCode { get; private set; }

    /// <summary>Boleta (to the guest) or Factura (to a company with RUC).</summary>
    public ReceiptType Type { get; private set; }

    public string Series { get; private set; }

    /// <summary>Correlative number of the series in the hotel.</summary>
    public int Number { get; private set; }

    /// <summary>Printed number, e.g. <c>B001-00000012</c>.</summary>
    public string FullNumber => $"{Series}-{Number:D8}";

    /// <summary>The payment it documents: the booking payment or a payment of the folio.</summary>
    public ReceiptSource Source { get; private set; }
    public int SourceId { get; private set; }

    public decimal Total { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal Igv { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string Concept { get; private set; }

    /// <summary>The guest (boleta) or the company (factura).</summary>
    public string CustomerName { get; private set; }

    /// <summary>The RUC of the company (factura only).</summary>
    public string? CustomerDocument { get; private set; }

    public string? CustomerAddress { get; private set; }

    /// <summary>Where it is sent.</summary>
    public string CustomerEmail { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    /// <summary>How many times it was e-mailed (the issue included), and the last time.</summary>
    public int SentCount { get; private set; }
    public DateTimeOffset? LastSentAt { get; private set; }

    /// <summary>The receipt was (re)sent to <see cref="CustomerEmail"/>.</summary>
    public void RecordSent(DateTimeOffset now)
    {
        SentCount++;
        LastSentAt = now;
    }
}
