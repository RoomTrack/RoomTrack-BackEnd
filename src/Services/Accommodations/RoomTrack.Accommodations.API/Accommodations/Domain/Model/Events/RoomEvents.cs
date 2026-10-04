using BackendAwRoomTrack.API.Accommodations.Domain.Model.ValueObjects;
using BackendAwRoomTrack.Domain.Shared.Domain.Model.Events;

namespace BackendAwRoomTrack.API.Accommodations.Domain.Model.Events;

/// <summary>The status of a room changed.</summary>
public sealed record RoomStatusChangedEvent(int RoomId, int HotelId, RoomStatus FromStatus, RoomStatus ToStatus,
    RoomStatusChangeOrigin Origin, int? ChangedByUserId, DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>A room has been under maintenance longer than allowed.</summary>
public sealed record RoomMaintenanceOverdueEvent(int RoomId, int HotelId, DateTimeOffset MaintenanceSince, DateTimeOffset OccurredOn)
    : DomainEvent(OccurredOn);
