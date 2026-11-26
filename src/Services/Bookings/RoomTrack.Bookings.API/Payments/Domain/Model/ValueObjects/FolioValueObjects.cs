using System.Text.RegularExpressions;
using RoomTrack.Payments.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Payments.Domain.Model.ValueObjects;

/// <summary>Open: charges may be pending. Settled ("saldado"): everything charged was paid. Closed: after check-out.</summary>
public enum FolioStatus
{
    Open,
    Settled,
    Closed
}

/// <summary>What an additional charge is for.</summary>
public enum FolioChargeCategory
{
    Minibar,
    RoomService,
    Restaurant,
    Laundry,
    Spa,
    Damage,
    Other
}

/// <summary>The guarantee deposit of a stay (US-15/16).</summary>
public enum DepositStatus
{
    /// <summary>The hotel held no deposit.</summary>
    None,
    /// <summary>Held at check-in, refundable.</summary>
    Held,
    /// <summary>Released at check-out: its refund started.</summary>
    Released
}

/// <summary>Boleta (consumer receipt) or Factura (invoice to a company with RUC), per SUNAT.</summary>
public enum ReceiptType
{
    Boleta,
    Factura
}

/// <summary>The payment a receipt documents.</summary>
public enum ReceiptSource
{
    /// <summary>The payment of the booking (the room).</summary>
    BookingPayment,
    /// <summary>A payment of the additional charges of the folio.</summary>
    FolioPayment
}

/// <summary>One part of a (split) payment of a folio.</summary>
/// <param name="Amount">How much is paid with this method.</param>
/// <param name="Method">How.</param>
/// <param name="OperationNumber">The operation number (every method except cash).</param>
public sealed record FolioPaymentPart(decimal Amount, PaymentMethod Method, string? OperationNumber);

/// <summary>The company an invoice (factura) is issued to (US-17).</summary>
public sealed partial record InvoiceData
{
    public const int MaxBusinessNameLength = 150;
    public const int MaxAddressLength = 200;

    private InvoiceData(string ruc, string businessName, string? address)
    {
        Ruc = ruc;
        BusinessName = businessName;
        Address = address;
    }

    /// <summary>RUC: 11 digits starting with 10, 15, 17 or 20.</summary>
    public string Ruc { get; }

    /// <summary>Razón social.</summary>
    public string BusinessName { get; }

    /// <summary>Fiscal address, optional.</summary>
    public string? Address { get; }

    /// <summary>Validates the invoice data; every broken rule is reported at once.</summary>
    public static InvoiceData Create(string? ruc, string? businessName, string? address)
    {
        var violations = new List<FieldViolation>();
        var trimmedRuc = ruc?.Trim() ?? string.Empty;
        if (!RucPattern().IsMatch(trimmedRuc))
            violations.Add(new FieldViolation("ruc", FolioErrorCodes.InvalidRuc,
                "The RUC has 11 digits and starts with 10, 15, 17 or 20."));
        var name = businessName?.Trim() ?? string.Empty;
        if (name.Length is 0 or > MaxBusinessNameLength)
            violations.Add(new FieldViolation("businessName", FolioErrorCodes.BusinessNameLength,
                $"Enter the business name (up to {MaxBusinessNameLength} characters).",
                new Dictionary<string, object?> { ["maxLength"] = MaxBusinessNameLength }));
        var trimmedAddress = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        if (trimmedAddress is { Length: > MaxAddressLength })
            violations.Add(new FieldViolation("address", FolioErrorCodes.AddressLength,
                $"The address cannot exceed {MaxAddressLength} characters.",
                new Dictionary<string, object?> { ["maxLength"] = MaxAddressLength }));
        if (violations.Count > 0) throw new InvalidFieldsException(violations);
        return new InvoiceData(trimmedRuc, name, trimmedAddress);
    }

    /// <summary>Rehydrates stored data (EF Core), without validating it again.</summary>
    internal static InvoiceData Restore(string ruc, string businessName, string? address) => new(ruc, businessName, address);

    [GeneratedRegex("^(10|15|17|20)[0-9]{9}$")]
    private static partial Regex RucPattern();
}
