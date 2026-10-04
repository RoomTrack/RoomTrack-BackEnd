using BackendAwRoomTrack.API.Accommodations.Domain.Model.ValueObjects;
using BackendAwRoomTrack.Domain.Shared.Domain.Model.Exceptions;

namespace BackendAwRoomTrack.API.Accommodations.Domain.Model.Exceptions;

/// <summary>The requested status change is not a valid transition from the current status.</summary>
public class InvalidRoomStatusTransitionException(int roomId, RoomStatus from, RoomStatus to)
    : BusinessRuleViolationException(AccommodationErrorCodes.RoomInvalidStatusTransition,
        $"Room {roomId} cannot change from {from} to {to}. Allowed from {from}: {string.Join(", ", RoomStatusTransitions.AllowedFrom(from))}.");
