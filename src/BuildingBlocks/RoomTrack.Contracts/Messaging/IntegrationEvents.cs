namespace RoomTrack.Contracts.Messaging;

// Integration events: facts one service publishes on RabbitMQ for the others. They carry the state the consumers
// need (event-carried state transfer), so no consumer has to call the publisher back. Consumers must be idempotent
// and tolerate reordering: every event carries the instant of its fact (OccurredOn) so only the latest state is kept.

/// <summary>A room was created in a hotel (Accommodations).</summary>
public sealed record RoomRegisteredIntegrationEvent(int RoomId, int HotelId, DateTimeOffset OccurredOn);

/// <summary>A room was deleted (Accommodations).</summary>
public sealed record RoomRemovedIntegrationEvent(int RoomId, int HotelId, DateTimeOffset OccurredOn);

/// <summary>
///     The current state of a booking after any change: created, confirmed, cancelled, rescheduled, checked in or
///     expired (Bookings).
/// </summary>
public sealed record BookingStateChangedIntegrationEvent(
    int BookingId,
    int HotelId,
    int RoomId,
    DateTime CheckInDate,
    DateTime CheckOutDate,
    string Status,
    DateTimeOffset OccurredOn);

/// <summary>The current state of the payment of a booking after it was completed or refunded (Bookings/Payments).</summary>
public sealed record PaymentStateChangedIntegrationEvent(
    int PaymentId,
    int BookingId,
    int HotelId,
    decimal Amount,
    DateTime PaymentDate,
    string Status,
    DateTimeOffset OccurredOn);
