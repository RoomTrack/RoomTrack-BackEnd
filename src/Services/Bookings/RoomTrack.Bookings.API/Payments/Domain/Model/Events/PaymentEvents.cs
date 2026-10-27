using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Events;

namespace RoomTrack.Payments.Domain.Model.Events;

/// <summary>The payment of a booking was registered and approved.</summary>
public sealed record PaymentCompletedEvent(int PaymentId, int BookingId, decimal Amount, PaymentMethod Method, DateTimeOffset OccurredOn)
    : DomainEvent(OccurredOn);

/// <summary>A completed payment was marked refunded because its booking was cancelled (R2).</summary>
public sealed record PaymentRefundedEvent(int PaymentId, int BookingId, decimal Amount, DateTimeOffset OccurredOn)
    : DomainEvent(OccurredOn);
