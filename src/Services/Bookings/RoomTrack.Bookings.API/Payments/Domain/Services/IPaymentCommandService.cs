using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.Commands;

namespace RoomTrack.Payments.Domain.Services;

/// <summary>Payment use cases.</summary>
public interface IPaymentCommandService
{
    /// <summary>Registers the payment of a Pending booking and, when approved, confirms the booking.</summary>
    Task<Payment> Handle(RegisterPaymentCommand command);

    /// <summary>
    ///     The guest pays their Pending booking with a card through the card gateway (US-15); when approved the booking is
    ///     confirmed. A declined card throws <c>CardPaymentDeclinedException</c> after keeping the failed attempt.
    /// </summary>
    Task<Payment> Handle(PayBookingByCardCommand command);
}
