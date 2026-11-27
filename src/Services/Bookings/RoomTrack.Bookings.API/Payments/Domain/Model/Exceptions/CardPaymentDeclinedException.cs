using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Payments.Domain.Model.Exceptions;

/// <summary>
///     The card gateway rejected the card of the guest (US-15). The failed attempt is kept and the booking stays Pending,
///     so the guest can try another card or another method (409 with <c>params</c>: <c>paymentId</c>,
///     <c>declineCode</c>, <c>reason</c> and the <c>alternatives</c> the hotel accepts).
/// </summary>
public class CardPaymentDeclinedException : BusinessRuleViolationException
{
    public CardPaymentDeclinedException(int paymentId, string declineCode, string reason, IReadOnlyList<string> alternatives)
        : base(PaymentErrorCodes.CardDeclined, $"The card was declined: {reason}")
    {
        PaymentId = paymentId;
        DeclineCode = declineCode;
        Reason = reason;
        Alternatives = alternatives;
        Parameters = new Dictionary<string, object?>
        {
            ["paymentId"] = paymentId,
            ["declineCode"] = declineCode,
            ["reason"] = reason,
            ["alternatives"] = alternatives
        };
    }

    public int PaymentId { get; }
    public string DeclineCode { get; }
    public string Reason { get; }

    /// <summary>The methods the guest can pay with instead (another card first).</summary>
    public IReadOnlyList<string> Alternatives { get; }
}
