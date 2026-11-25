using RoomTrack.Payments.Domain.Model.Events;
using RoomTrack.Payments.Domain.Model.Exceptions;
using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Events;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Payments.Domain.Model.Aggregates;

/// <summary>
///     The folio of a stay (US-16): the additional charges posted during the stay (minibar, laundry, room service…),
///     what the guest paid of them (possibly split between several methods), the guarantee deposit held at check-in
///     (US-15) and the invoice data of the guest (US-17). The room itself is paid by the booking payment (D1), not here.
/// </summary>
/// <remarks>
///     The check-out is refused while the folio has a balance; at check-out the folio is closed and a held deposit is
///     released (its refund starts). One folio per booking.
/// </remarks>
public class GuestFolio : IHasDomainEvents
{
    public const int MaxEmailLength = 254;
    public const int MaxGuestNameLength = 150;
    public const int MaxPartsPerPayment = 4;

    private readonly List<IEvent> _domainEvents = [];
    private readonly List<FolioCharge> _charges = [];
    private readonly List<FolioPayment> _payments = [];
    private readonly List<FolioPayment> _paymentsAwaitingId = [];

    /// <summary>EF Core constructor.</summary>
    protected GuestFolio()
    {
        BookingCode = string.Empty;
        GuestEmail = string.Empty;
        GuestName = string.Empty;
    }

    /// <summary>Opens the folio of a booking (on first use: a charge, a deposit, the invoice data or a read).</summary>
    public static GuestFolio Open(int bookingId, int hotelId, string bookingCode, string guestName, string guestEmail,
        DateTimeOffset now) => new()
    {
        BookingId = bookingId,
        HotelId = hotelId,
        BookingCode = bookingCode,
        GuestName = guestName.Length > MaxGuestNameLength ? guestName[..MaxGuestNameLength] : guestName,
        GuestEmail = guestEmail.Length > MaxEmailLength ? guestEmail[..MaxEmailLength] : guestEmail,
        Status = FolioStatus.Open,
        OpenedAt = now
    };

    public int Id { get; private set; }
    public int BookingId { get; private set; }
    public int HotelId { get; private set; }
    public string BookingCode { get; private set; }
    public string GuestName { get; private set; }
    public string GuestEmail { get; private set; }

    /// <summary>Open (balance pending or nothing paid yet), Settled ("saldado") or Closed (after the check-out).</summary>
    public FolioStatus Status { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>The guarantee deposit held at check-in (US-15), if any.</summary>
    public decimal? DepositAmount { get; private set; }
    public DepositStatus DepositStatus { get; private set; }
    public PaymentMethod? DepositMethod { get; private set; }
    public string? DepositOperationNumber { get; private set; }
    public DateTimeOffset? DepositHeldAt { get; private set; }
    public DateTimeOffset? DepositReleasedAt { get; private set; }

    /// <summary>The company the guest wants the invoice (factura) issued to (US-17); null: receipts are boletas.</summary>
    public InvoiceData? InvoiceData { get; private set; }

    /// <summary>The additional charges, oldest first.</summary>
    public IReadOnlyList<FolioCharge> Charges => _charges.AsReadOnly();

    /// <summary>What the guest paid of the charges, oldest first.</summary>
    public IReadOnlyList<FolioPayment> Payments => _payments.AsReadOnly();

    public decimal ChargesTotal => _charges.Sum(charge => charge.Amount);
    public decimal PaidTotal => _payments.Sum(payment => payment.Amount);

    /// <summary>What the guest still owes (never negative).</summary>
    public decimal Balance => Math.Max(0m, ChargesTotal - PaidTotal);

    public IReadOnlyCollection<IEvent> DomainEvents
    {
        get
        {
            // A payment's receipt needs its generated id: the event is recorded once the payment has it.
            foreach (var payment in _paymentsAwaitingId.Where(payment => payment.Id > 0).ToList())
            {
                _domainEvents.Add(new FolioPaymentRecordedEvent(Id, payment.Id, BookingId, HotelId, payment.Amount, payment.Method,
                    payment.PaidAt));
                _paymentsAwaitingId.Remove(payment);
            }
            return _domainEvents.AsReadOnly();
        }
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>The front desk posts an additional charge of the stay (minibar, laundry, room service…).</summary>
    public FolioCharge PostCharge(string? description, FolioChargeCategory category, decimal amount, int postedByUserId,
        DateTimeOffset now)
    {
        EnsureNotClosed();
        var charge = new FolioCharge(FolioCharge.NormalizeDescription(description), category, FolioMoney.Positive(amount, "amount"),
            postedByUserId, now);
        _charges.Add(charge);
        Status = FolioStatus.Open;
        _domainEvents.Add(new FolioChargePostedEvent(Id, BookingId, HotelId, charge.Description, charge.Amount, now));
        return charge;
    }

    /// <summary>
    ///     US-15: the hotel holds a refundable guarantee deposit at check-in. It is visible in the folio and released at
    ///     check-out.
    /// </summary>
    public void HoldDeposit(decimal amount, PaymentMethod method, string? operationNumber, DateTimeOffset now)
    {
        EnsureNotClosed();
        if (DepositStatus == DepositStatus.Held)
            throw new BusinessRuleViolationException(FolioErrorCodes.DepositAlreadyHeld,
                $"The folio of booking {BookingCode} already holds a deposit of {DepositAmount:0.00}.");
        DepositAmount = FolioMoney.Positive(amount, "amount");
        DepositMethod = method;
        DepositOperationNumber = FolioMoney.OperationNumber(method, operationNumber);
        DepositStatus = DepositStatus.Held;
        DepositHeldAt = now;
        DepositReleasedAt = null;
        _domainEvents.Add(new DepositHeldEvent(Id, BookingId, HotelId, DepositAmount.Value, method, now));
    }

    /// <summary>
    ///     The guest pays (part of) the balance, possibly split between several methods (US-16). Each part is a payment
    ///     of its own (with its own receipt); together they cannot exceed the balance. A folio paid in full becomes
    ///     Settled.
    /// </summary>
    public IReadOnlyList<FolioPayment> Pay(IReadOnlyList<FolioPaymentPart> parts, int? recordedByUserId, DateTimeOffset now)
    {
        EnsureNotClosed();
        if (parts.Count is 0 or > MaxPartsPerPayment)
            throw new InvalidFieldException("parts", FolioErrorCodes.PaymentPartsCount,
                $"Pay with between 1 and {MaxPartsPerPayment} methods.",
                new Dictionary<string, object?> { ["maxParts"] = MaxPartsPerPayment });
        var balance = Balance;
        if (balance == 0)
            throw new BusinessRuleViolationException(FolioErrorCodes.NothingToPay,
                $"The folio of booking {BookingCode} has no balance to pay.");

        var payments = parts.Select(part => new FolioPayment(FolioMoney.Positive(part.Amount, "amount"), part.Method,
            FolioMoney.OperationNumber(part.Method, part.OperationNumber), recordedByUserId, now)).ToList();
        var total = payments.Sum(payment => payment.Amount);
        if (total > balance) throw new FolioOverpaymentException(balance, total);

        _payments.AddRange(payments);
        _paymentsAwaitingId.AddRange(payments);
        if (Balance == 0)
        {
            Status = FolioStatus.Settled;
            _domainEvents.Add(new FolioSettledEvent(Id, BookingId, HotelId, ChargesTotal, now));
        }
        return payments;
    }

    /// <summary>US-17: the guest asks for an invoice (factura) before the check-out: the receipts that follow are facturas.</summary>
    public void RequestInvoice(InvoiceData invoiceData)
    {
        EnsureNotClosed();
        InvoiceData = invoiceData;
    }

    /// <summary>US-14/16: the guest cannot check out while the folio has a balance; the pending charges are listed.</summary>
    public void EnsureSettledForCheckOut()
    {
        if (Balance > 0) throw new FolioBalancePendingException(BookingCode, Balance, _charges);
    }

    /// <summary>
    ///     The guest checked out: the folio is closed and a held deposit is released (US-16: its refund starts
    ///     automatically). Closing a closed folio is a no-op.
    /// </summary>
    public void CloseForCheckOut(DateTimeOffset now)
    {
        if (Status == FolioStatus.Closed) return;
        EnsureSettledForCheckOut();
        Status = FolioStatus.Closed;
        ClosedAt = now;
        if (DepositStatus == DepositStatus.Held)
        {
            DepositStatus = DepositStatus.Released;
            DepositReleasedAt = now;
            _domainEvents.Add(new DepositReleasedEvent(Id, BookingId, HotelId, DepositAmount!.Value, DepositMethod!.Value, now));
        }
        _domainEvents.Add(new FolioClosedEvent(Id, BookingId, HotelId, ChargesTotal, PaidTotal, now));
    }

    private void EnsureNotClosed()
    {
        if (Status == FolioStatus.Closed)
            throw new BusinessRuleViolationException(FolioErrorCodes.Closed,
                $"The folio of booking {BookingCode} was closed at check-out.");
    }
}

/// <summary>An additional charge of a stay.</summary>
public class FolioCharge
{
    public const int MaxDescriptionLength = 150;

    /// <summary>EF Core constructor.</summary>
    protected FolioCharge()
    {
        Description = string.Empty;
    }

    internal FolioCharge(string description, FolioChargeCategory category, decimal amount, int postedByUserId, DateTimeOffset postedAt)
    {
        Description = description;
        Category = category;
        Amount = amount;
        PostedByUserId = postedByUserId;
        PostedAt = postedAt;
    }

    public long Id { get; private set; }
    public int GuestFolioId { get; private set; }
    public string Description { get; private set; }
    public FolioChargeCategory Category { get; private set; }
    public decimal Amount { get; private set; }
    public int PostedByUserId { get; private set; }
    public DateTimeOffset PostedAt { get; private set; }

    internal static string NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxDescriptionLength)
            throw new InvalidFieldException("description", FolioErrorCodes.ChargeDescriptionLength,
                $"Describe the charge in up to {MaxDescriptionLength} characters.",
                new Dictionary<string, object?> { ["maxLength"] = MaxDescriptionLength });
        return trimmed;
    }
}

/// <summary>A payment of (part of) the balance of a folio.</summary>
public class FolioPayment
{
    /// <summary>EF Core constructor.</summary>
    protected FolioPayment() { }

    internal FolioPayment(decimal amount, PaymentMethod method, string? operationNumber, int? recordedByUserId, DateTimeOffset paidAt)
    {
        Amount = amount;
        Method = method;
        OperationNumber = operationNumber;
        RecordedByUserId = recordedByUserId;
        PaidAt = paidAt;
    }

    public int Id { get; private set; }
    public int GuestFolioId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string? OperationNumber { get; private set; }

    /// <summary>The staff member who registered it (null: the guest from the app).</summary>
    public int? RecordedByUserId { get; private set; }

    public DateTimeOffset PaidAt { get; private set; }
}

/// <summary>Money rules of the folio.</summary>
internal static class FolioMoney
{
    public const decimal MaxAmount = 100_000m;

    public static decimal Positive(decimal amount, string field)
    {
        var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (rounded <= 0 || rounded > MaxAmount)
            throw new InvalidFieldException(field, FolioErrorCodes.AmountRange,
                $"The amount must be greater than 0 and at most {MaxAmount:0.00}.",
                new Dictionary<string, object?> { ["max"] = MaxAmount });
        return rounded;
    }

    public static string? OperationNumber(PaymentMethod method, string? operationNumber)
    {
        var operation = string.IsNullOrWhiteSpace(operationNumber) ? null : operationNumber.Trim();
        if (method == PaymentMethod.Cash) return null;
        if (operation is null)
            throw new InvalidFieldException("operationNumber", PaymentErrorCodes.OperationNumberRequired,
                $"Enter the operation number of the {method} payment.");
        if (operation.Length > Payment.MaxOperationNumberLength)
            throw new InvalidFieldException("operationNumber", PaymentErrorCodes.OperationNumberTooLong,
                $"The operation number cannot exceed {Payment.MaxOperationNumberLength} characters.");
        return operation;
    }
}
