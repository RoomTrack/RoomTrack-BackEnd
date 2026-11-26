using System.ComponentModel.DataAnnotations;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Infrastructure.Interfaces.ASP.Validation;

namespace RoomTrack.Payments.Interfaces.REST.Resources;

/// <summary>An additional charge of the stay (US-16).</summary>
public record PostFolioChargeResource : IValidatableObject
{
    /// <summary>What was consumed (up to 150 characters).</summary>
    /// <example>Minibar: 2 aguas y 1 snack</example>
    [Required]
    public string? Description { get; init; }

    /// <summary>Minibar, RoomService, Restaurant, Laundry, Spa, Damage or Other (Other by default).</summary>
    /// <example>Minibar</example>
    public string? Category { get; init; }

    /// <summary>Amount in soles, IGV included (greater than 0).</summary>
    /// <example>18.50</example>
    [Required]
    public decimal? Amount { get; init; }

    public FolioChargeCategory ToCategory() =>
        string.IsNullOrWhiteSpace(Category) ? FolioChargeCategory.Other : Enum.Parse<FolioChargeCategory>(Category, ignoreCase: true);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        EnumValidation.Validate<FolioChargeCategory>(Category, "category");
}

/// <summary>The refundable guarantee deposit held at check-in (US-15).</summary>
public record HoldDepositResource : IValidatableObject
{
    /// <summary>Amount in soles.</summary>
    /// <example>200.00</example>
    [Required]
    public decimal? Amount { get; init; }

    /// <summary>Yape, Plin, BankTransfer, Cash or CardAtFrontDesk.</summary>
    /// <example>CardAtFrontDesk</example>
    [Required]
    public string? Method { get; init; }

    /// <summary>Operation number (every method except Cash).</summary>
    /// <example>POS-778812</example>
    [MaxLength(50)]
    public string? OperationNumber { get; init; }

    public PaymentMethod ToMethod() => Enum.Parse<PaymentMethod>(Method!, ignoreCase: true);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        EnumValidation.Validate<PaymentMethod>(Method, "method");
}

/// <summary>One part of a (split) payment.</summary>
public record FolioPaymentPartResource : IValidatableObject
{
    /// <summary>How much is paid with this method.</summary>
    /// <example>30.00</example>
    [Required]
    public decimal? Amount { get; init; }

    /// <summary>Yape, Plin, BankTransfer, Cash or CardAtFrontDesk.</summary>
    /// <example>Yape</example>
    [Required]
    public string? Method { get; init; }

    /// <summary>Operation number (every method except Cash).</summary>
    /// <example>00987654</example>
    [MaxLength(50)]
    public string? OperationNumber { get; init; }

    public FolioPaymentPart ToPart() =>
        new(Amount!.Value, Enum.Parse<PaymentMethod>(Method!, ignoreCase: true), OperationNumber);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        EnumValidation.Validate<PaymentMethod>(Method, "method");
}

/// <summary>A payment of the balance of the folio, possibly split between methods (US-16).</summary>
public record PayFolioResource
{
    /// <summary>The parts (1 to 4); together at most the balance.</summary>
    [Required]
    public IReadOnlyList<FolioPaymentPartResource>? Parts { get; init; }
}

/// <summary>The company the invoice (factura) is issued to (US-17).</summary>
public record InvoiceDataResource
{
    /// <summary>RUC: 11 digits starting with 10, 15, 17 or 20.</summary>
    /// <example>20123456789</example>
    [Required]
    public string? Ruc { get; init; }

    /// <summary>Razón social (up to 150 characters).</summary>
    /// <example>Inversiones Andinas S.A.C.</example>
    [Required]
    public string? BusinessName { get; init; }

    /// <summary>Fiscal address, optional (up to 200 characters).</summary>
    /// <example>Av. Larco 123, Miraflores, Lima</example>
    public string? Address { get; init; }
}

/// <summary>An additional charge of the folio.</summary>
/// <param name="Id">Charge id.</param>
/// <param name="Description">What was consumed.</param>
/// <param name="Category">Minibar, RoomService, Restaurant, Laundry, Spa, Damage or Other.</param>
/// <param name="Amount">Amount (IGV included).</param>
/// <param name="PostedByUserId">Who posted it.</param>
/// <param name="PostedAt">When (UTC).</param>
public record FolioChargeResource(long Id, string Description, string Category, decimal Amount, int PostedByUserId,
    DateTimeOffset PostedAt);

/// <summary>A payment of the folio.</summary>
/// <param name="Id">Payment id.</param>
/// <param name="Amount">Amount paid.</param>
/// <param name="Method">How.</param>
/// <param name="OperationNumber">Operation number.</param>
/// <param name="PaidAt">When (UTC).</param>
public record FolioPaymentResource(int Id, decimal Amount, string Method, string? OperationNumber, DateTimeOffset PaidAt);

/// <summary>The guarantee deposit of the stay.</summary>
/// <param name="Amount">Amount held.</param>
/// <param name="Status">Held or Released (its refund started).</param>
/// <param name="Method">How it was paid (and how it is refunded).</param>
/// <param name="HeldAt">When it was held.</param>
/// <param name="ReleasedAt">When it was released.</param>
public record FolioDepositResource(decimal Amount, string Status, string? Method, DateTimeOffset? HeldAt, DateTimeOffset? ReleasedAt);

/// <summary>The folio of a stay (US-16).</summary>
/// <param name="BookingId">The booking.</param>
/// <param name="BookingCode">Its code.</param>
/// <param name="Status">Open, Settled ("saldado") or Closed (after the check-out).</param>
/// <param name="ChargesTotal">Sum of the charges.</param>
/// <param name="PaidTotal">Sum of the payments.</param>
/// <param name="Balance">What the guest still owes; the check-out is refused while it is above 0.</param>
/// <param name="Charges">The charges, oldest first.</param>
/// <param name="Payments">The payments, oldest first.</param>
/// <param name="Deposit">The guarantee deposit, if any.</param>
/// <param name="InvoiceData">The company of the factura, if the guest asked for one.</param>
public record GuestFolioResource(int BookingId, string BookingCode, string Status, decimal ChargesTotal, decimal PaidTotal,
    decimal Balance, IReadOnlyList<FolioChargeResource> Charges, IReadOnlyList<FolioPaymentResource> Payments,
    FolioDepositResource? Deposit, InvoiceDataView? InvoiceData);

/// <summary>Invoice data of a folio.</summary>
/// <param name="Ruc">RUC.</param>
/// <param name="BusinessName">Razón social.</param>
/// <param name="Address">Fiscal address.</param>
public record InvoiceDataView(string Ruc, string BusinessName, string? Address);

/// <summary>An electronic receipt (US-17).</summary>
/// <param name="Id">Receipt id.</param>
/// <param name="Number">Printed number, e.g. B001-00000012.</param>
/// <param name="Type">Boleta or Factura.</param>
/// <param name="BookingId">The booking.</param>
/// <param name="BookingCode">Its code.</param>
/// <param name="Source">BookingPayment or FolioPayment.</param>
/// <param name="Concept">What it documents.</param>
/// <param name="Currency">PEN.</param>
/// <param name="Subtotal">Taxable amount.</param>
/// <param name="Igv">IGV (18 %).</param>
/// <param name="Total">Total paid.</param>
/// <param name="Method">How it was paid.</param>
/// <param name="CustomerName">The guest or the company.</param>
/// <param name="CustomerDocument">The RUC (factura).</param>
/// <param name="CustomerAddress">Fiscal address (factura).</param>
/// <param name="CustomerEmail">Where it was sent.</param>
/// <param name="IssuedAt">When (UTC).</param>
/// <param name="SentCount">How many times it was e-mailed.</param>
/// <param name="LastSentAt">The last time.</param>
public record ReceiptResource(int Id, string Number, string Type, int BookingId, string BookingCode, string Source, string Concept,
    string Currency, decimal Subtotal, decimal Igv, decimal Total, string Method, string CustomerName, string? CustomerDocument,
    string? CustomerAddress, string CustomerEmail, DateTimeOffset IssuedAt, int SentCount, DateTimeOffset? LastSentAt);

/// <summary>The folio after a payment, with the receipts it produced.</summary>
/// <param name="Folio">The updated folio.</param>
/// <param name="Receipts">One receipt per part of the payment.</param>
public record FolioPaymentResultResource(GuestFolioResource Folio, IReadOnlyList<ReceiptResource> Receipts);

/// <summary>One line of the payment history (US-17).</summary>
/// <param name="OccurredAt">When.</param>
/// <param name="Type">BookingPayment, Refund, FolioPayment, DepositHeld, DepositReleased or Receipt.</param>
/// <param name="BookingId">The booking.</param>
/// <param name="Amount">Money in (positive) or out (negative).</param>
/// <param name="Method">How.</param>
/// <param name="Reference">Operation number, transaction or receipt number.</param>
/// <param name="ReceiptId">The receipt (Receipt lines).</param>
public record PaymentHistoryEntryResource(DateTimeOffset OccurredAt, string Type, int BookingId, decimal Amount, string? Method,
    string? Reference, int? ReceiptId);

internal static class EnumValidation
{
    public static IEnumerable<ValidationResult> Validate<TEnum>(string? value, string field) where TEnum : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(value)
            && (int.TryParse(value, out _) || !Enum.TryParse<TEnum>(value, true, out var parsed) || !Enum.IsDefined(parsed)))
            yield return new CodedValidationResult(ErrorCodes.FieldNotAllowed,
                $"{char.ToUpperInvariant(field[0])}{field[1..]} must be one of: {string.Join(", ", Enum.GetNames<TEnum>())}.", [field],
                new Dictionary<string, object?> { ["allowed"] = Enum.GetNames<TEnum>() });
    }
}
