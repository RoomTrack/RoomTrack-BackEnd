namespace RoomTrack.Payments.Domain.Model.Exceptions;

/// <summary>Stable error codes of the Payments context (payments registered by the hotel).</summary>
public static class PaymentErrorCodes
{
    public const string OperationNumberRequired = "payment.operation_number_required";
    public const string OperationNumberTooLong = "payment.operation_number_too_long";
    public const string NoteTooLong = "payment.note_too_long";
    public const string OutsideHotelScope = "payment.outside_hotel_scope";
    public const string BookingAlreadyPaid = "payment.booking_already_paid";
    public const string BookingNotPending = "payment.booking_not_pending";
    public const string CannotComplete = "payment.cannot_complete";
    public const string CannotFail = "payment.cannot_fail";

    /// <summary>An invariant of the Payment aggregate that only a programming error can break.</summary>
    public const string InternalInvariant = "payment.internal_invariant";
}

/// <summary>Stable error codes of the folios and receipts (US-16, US-17).</summary>
public static class FolioErrorCodes
{
    public const string Closed = "folio.closed";
    public const string BalancePending = "folio.balance_pending";
    public const string NothingToPay = "folio.nothing_to_pay";
    public const string Overpayment = "folio.overpayment";
    public const string PaymentPartsCount = "folio.payment_parts_count";
    public const string AmountRange = "folio.amount_range";
    public const string ChargeDescriptionLength = "folio.charge_description_length";
    public const string DepositAlreadyHeld = "folio.deposit_already_held";
    public const string BookingNotStaying = "folio.booking_not_staying";
    public const string InvalidRuc = "invoice.invalid_ruc";
    public const string BusinessNameLength = "invoice.business_name_length";
    public const string AddressLength = "invoice.address_length";
}
