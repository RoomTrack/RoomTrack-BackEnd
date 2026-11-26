using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Events;

namespace RoomTrack.Payments.Domain.Model.Events;

/// <summary>The payment of a booking was registered and approved.</summary>
public sealed record PaymentCompletedEvent(int PaymentId, int BookingId, decimal Amount, PaymentMethod Method, DateTimeOffset OccurredOn)
    : DomainEvent(OccurredOn);

/// <summary>A completed payment was marked refunded because its booking was cancelled (R2).</summary>
public sealed record PaymentRefundedEvent(int PaymentId, int BookingId, decimal Amount, DateTimeOffset OccurredOn)
    : DomainEvent(OccurredOn);

/// <summary>An additional charge was posted to the folio of a stay (US-16).</summary>
public sealed record FolioChargePostedEvent(int FolioId, int BookingId, int HotelId, string Description, decimal Amount,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>A payment of the folio was recorded (one per part of a split payment); its receipt is issued (US-17).</summary>
public sealed record FolioPaymentRecordedEvent(int FolioId, int FolioPaymentId, int BookingId, int HotelId, decimal Amount,
    PaymentMethod Method, DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>Everything charged to the folio is paid: it is "saldado".</summary>
public sealed record FolioSettledEvent(int FolioId, int BookingId, int HotelId, decimal ChargesTotal, DateTimeOffset OccurredOn)
    : DomainEvent(OccurredOn);

/// <summary>US-15: a refundable guarantee deposit was held.</summary>
public sealed record DepositHeldEvent(int FolioId, int BookingId, int HotelId, decimal Amount, PaymentMethod Method,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>US-16: the deposit was released at check-out: its refund started.</summary>
public sealed record DepositReleasedEvent(int FolioId, int BookingId, int HotelId, decimal Amount, PaymentMethod Method,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>The folio was closed at check-out; the guest gets the statement of the stay.</summary>
public sealed record FolioClosedEvent(int FolioId, int BookingId, int HotelId, decimal ChargesTotal, decimal PaidTotal,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);
