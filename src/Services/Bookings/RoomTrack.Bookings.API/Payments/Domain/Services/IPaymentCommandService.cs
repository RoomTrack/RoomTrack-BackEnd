using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.Commands;

namespace RoomTrack.Payments.Domain.Services;

/// <summary>Payment use cases.</summary>
public interface IPaymentCommandService
{
    /// <summary>Registers the payment of a Pending booking and, when approved, confirms the booking.</summary>
    Task<Payment> Handle(RegisterPaymentCommand command);
}
