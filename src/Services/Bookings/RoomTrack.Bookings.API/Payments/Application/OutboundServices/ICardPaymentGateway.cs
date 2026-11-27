namespace RoomTrack.Payments.Application.OutboundServices;

/// <summary>
///     A card charge for the total of a booking (US-15). The card itself never reaches the API: the app tokenizes it
///     with the gateway's SDK and sends only <paramref name="CardToken"/>.
/// </summary>
/// <param name="BookingId">The booking paid.</param>
/// <param name="BookingCode">Its code (the gateway's description of the charge).</param>
/// <param name="Amount">The booking total, in soles.</param>
/// <param name="CardToken">The token of the card, issued by the gateway to the app.</param>
/// <param name="IdempotencyKey">Same key, same charge: a retried request never charges the card twice.</param>
public sealed record CardCharge(int BookingId, string BookingCode, decimal Amount, string CardToken, string IdempotencyKey);

/// <summary>What the card gateway answered.</summary>
/// <param name="Approved">The card was charged.</param>
/// <param name="Reference">The gateway's reference of the charge (approved only).</param>
/// <param name="CardBrand">Brand of the card (VISA, MASTERCARD...), when the gateway knows it.</param>
/// <param name="Last4">Last 4 digits of the card, when the gateway knows them.</param>
/// <param name="DeclineCode">Why it was rejected, a stable code (insufficient_funds, expired_card, card_declined, invalid_token).</param>
/// <param name="DeclineReason">Why it was rejected, for the guest.</param>
public sealed record CardChargeResult(bool Approved, string? Reference, string? CardBrand, string? Last4, string? DeclineCode,
    string? DeclineReason)
{
    /// <summary>"VISA •••• 4242", what the payment and its receipt show of the card.</summary>
    public string? CardLabel => CardBrand is null || Last4 is null ? null : $"{CardBrand} •••• {Last4}";

    public static CardChargeResult Approve(string reference, string brand, string last4) => new(true, reference, brand, last4, null, null);

    public static CardChargeResult Decline(string code, string reason, string? brand = null, string? last4 = null) =>
        new(false, null, brand, last4, code, reason);
}

/// <summary>
///     Outbound port of the integrated card gateway (US-15): charges a tokenized card. Today the
///     <c>SandboxCardPaymentGateway</c> adapter simulates the gateway with its test tokens; a real provider (Culqi,
///     Mercado Pago, Niubiz) plugs in by implementing this interface and registering it, without touching the Payment
///     aggregate or the booking rules.
/// </summary>
public interface ICardPaymentGateway
{
    Task<CardChargeResult> ChargeAsync(CardCharge charge, CancellationToken cancellationToken = default);
}
