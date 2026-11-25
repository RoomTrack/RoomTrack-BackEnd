using RoomTrack.Shared.Domain.Model.Events;
using RoomTrack.ShiftHandovers.Domain.Model.ValueObjects;

namespace RoomTrack.ShiftHandovers.Domain.Model.Events;

/// <summary>A shift ended with a handover for the next shift of the area (recorded once it has its id).</summary>
public sealed record ShiftHandoverCreatedEvent(int ShiftHandoverId, int HotelId, ShiftArea Area, int AuthorUserId, int OpenItems,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

/// <summary>A colleague confirmed reading a handover.</summary>
public sealed record ShiftHandoverAcknowledgedEvent(int ShiftHandoverId, int HotelId, int AuthorUserId, int ReaderUserId,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);
