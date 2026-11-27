using RoomTrack.Payments.Application.OutboundServices;

namespace RoomTrack.Payments.Infrastructure.Gateways;

/// <summary>
///     The card gateway in sandbox mode (US-15): it never moves money and answers each test token the way a real gateway
///     answers the card behind it, so the app and the tests can go through every outcome. The same idempotency key
///     always gets the same answer.
/// </summary>
/// <remarks>
///     Test tokens (what the app's card form issues for the gateway's test cards):
///     <list type="table">
///         <item><term><c>tok_visa</c></term><description>VISA 4242, approved.</description></item>
///         <item><term><c>tok_mastercard</c></term><description>MASTERCARD 4444, approved.</description></item>
///         <item><term><c>tok_declined_insufficient_funds</c></term><description>Rejected: insufficient funds.</description></item>
///         <item><term><c>tok_declined_expired_card</c></term><description>Rejected: expired card.</description></item>
///         <item><term><c>tok_declined_card</c></term><description>Rejected by the issuing bank.</description></item>
///     </list>
///     Any other token is rejected as invalid (expired or not issued by the gateway).
/// </remarks>
public class SandboxCardPaymentGateway : ICardPaymentGateway
{
    public const string ApprovedVisa = "tok_visa";
    public const string ApprovedMastercard = "tok_mastercard";
    public const string DeclinedInsufficientFunds = "tok_declined_insufficient_funds";
    public const string DeclinedExpiredCard = "tok_declined_expired_card";
    public const string DeclinedCard = "tok_declined_card";

    public Task<CardChargeResult> ChargeAsync(CardCharge charge, CancellationToken cancellationToken = default) =>
        Task.FromResult(charge.CardToken.Trim() switch
        {
            ApprovedVisa => CardChargeResult.Approve(Reference(charge), "VISA", "4242"),
            ApprovedMastercard => CardChargeResult.Approve(Reference(charge), "MASTERCARD", "4444"),
            DeclinedInsufficientFunds => CardChargeResult.Decline("insufficient_funds",
                "La tarjeta no tiene fondos suficientes.", "VISA", "9995"),
            DeclinedExpiredCard => CardChargeResult.Decline("expired_card", "La tarjeta está vencida.", "VISA", "0069"),
            DeclinedCard => CardChargeResult.Decline("card_declined", "El banco emisor rechazó la tarjeta.", "VISA", "0002"),
            _ => CardChargeResult.Decline("invalid_token", "No se pudo leer la tarjeta: vuelve a ingresar sus datos.")
        });

    /// <summary>The charge reference, stable for the idempotency key.</summary>
    private static string Reference(CardCharge charge) =>
        $"chr_sbx_{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(charge.IdempotencyKey)))[..16].ToLowerInvariant()}";
}
