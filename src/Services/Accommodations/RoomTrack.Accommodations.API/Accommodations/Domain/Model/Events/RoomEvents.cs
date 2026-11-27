using RoomTrack.Accommodations.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Events;

namespace RoomTrack.Accommodations.Domain.Model.Events;

/// <summary>The status of a room changed.</summary>
public sealed record RoomStatusChangedEvent(int RoomId, int HotelId, RoomStatus FromStatus, RoomStatus ToStatus,
    RoomStatusChangeOrigin Origin, int? ChangedByUserId, DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>A guest arrives soon (at <paramref name="ExpectedAt"/>) and their room is still not ready (US-23).</summary>
public sealed record RoomNotReadyForArrivalEvent(int RoomId, int HotelId, int BookingId, string BookingCode, RoomStatus Status,
    DateTimeOffset ExpectedAt, DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>A room has been under maintenance longer than allowed.</summary>
public sealed record RoomMaintenanceOverdueEvent(int RoomId, int HotelId, DateTimeOffset MaintenanceSince, DateTimeOffset OccurredOn)
    : DomainEvent(OccurredOn);
