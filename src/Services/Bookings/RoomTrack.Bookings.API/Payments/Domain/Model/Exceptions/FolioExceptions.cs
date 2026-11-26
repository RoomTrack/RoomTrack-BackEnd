using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Payments.Domain.Model.Exceptions;

/// <summary>
///     US-14/16: the guest cannot check out with additional charges unpaid. The problem carries the balance and the
///     detail of the charges (<c>balance</c>, <c>charges</c>) so the app shows them before letting the guest finish.
/// </summary>
public class FolioBalancePendingException : BusinessRuleViolationException
{
    public FolioBalancePendingException(string bookingCode, decimal balance, IReadOnlyList<FolioCharge> charges)
        : base(FolioErrorCodes.BalancePending,
            $"Booking {bookingCode} has {balance:0.00} of additional charges pending: pay them before checking out.")
    {
        Balance = balance;
        Parameters = new Dictionary<string, object?>
        {
            ["balance"] = balance,
            ["charges"] = charges.Select(charge => new { charge.Description, Category = charge.Category.ToString(), charge.Amount, charge.PostedAt })
                .ToList()
        };
    }

    public decimal Balance { get; }
}

/// <summary>The parts of a payment add up to more than the balance of the folio.</summary>
public class FolioOverpaymentException : BusinessRuleViolationException
{
    public FolioOverpaymentException(decimal balance, decimal paid)
        : base(FolioErrorCodes.Overpayment, $"The payment ({paid:0.00}) exceeds the balance of the folio ({balance:0.00}).")
    {
        Parameters = new Dictionary<string, object?> { ["balance"] = balance, ["paid"] = paid };
    }
}
