using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Events;

namespace RoomTrack.GuestRequests.Domain.Model.Events;

/// <summary>
///     A request was registered (by the guest from the app, US-19, or by the front desk, US-11) for
///     <paramref name="Area"/>. Recorded once the request has its generated id.
/// </summary>
public sealed record GuestRequestCreatedEvent(int GuestRequestId, int HotelId, int RoomId, ServiceArea Area, int? GuestUserId,
    int CreatedByUserId, DateTimeOffset EstimatedAttentionBy, DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>The front desk derived the request to another area (US-11).</summary>
public sealed record GuestRequestRoutedEvent(int GuestRequestId, int HotelId, int RoomId, ServiceArea FromArea, ServiceArea ToArea,
    int RoutedByUserId, DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>A staff member is in charge of the request.</summary>
public sealed record GuestRequestAssignedEvent(int GuestRequestId, int HotelId, int RoomId, int AssigneeUserId, int AssignedByUserId,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>The status of the request changed (in progress, resolved or cancelled): the guest can follow it (US-19).</summary>
public sealed record GuestRequestStatusChangedEvent(int GuestRequestId, int HotelId, int RoomId, int? GuestUserId,
    GuestRequestStatus Status, int ChangedByUserId, DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);
