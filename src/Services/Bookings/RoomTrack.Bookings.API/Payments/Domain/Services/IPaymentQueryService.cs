using RoomTrack.Payments.Domain.Model.Aggregates;
using RoomTrack.Payments.Domain.Model.Queries;

namespace RoomTrack.Payments.Domain.Services;

/// <summary>
/// Defines the contract for services that retrieve payment data.
/// </summary>
public interface IPaymentQueryService
{
    Task<Payment?> Handle(GetPaymentByBookingIdQuery query);
}