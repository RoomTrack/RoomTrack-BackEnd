using BackendAwRoomTrack.API.Accommodations.Domain.Model.ValueObjects;
using BackendAwRoomTrack.Domain.Shared.Domain.Model.Exceptions;

namespace BackendAwRoomTrack.API.Accommodations.Domain.Model.Exceptions;

/// <summary>The requested status change is not a valid transition from the current status.</summary>
public class InvalidRoomStatusTransitionException(int roomId, RoomStatus from, RoomStatus to)
    : BusinessRuleViolationException(AccommodationErrorCodes.RoomInvalidStatusTransition,
        $"Room {roomId} cannot change from {from} to {to}. Allowed from {from}: {string.Join(", ", RoomStatusTransitions.AllowedFrom(from))}.");

/// <summary>An open incident blocks the room: it cannot be Available until the incident is resolved.</summary>
public class RoomBlockedByIncidentException(int roomId)
    : BusinessRuleViolationException(AccommodationErrorCodes.RoomBlockedByIncident,
        $"Room {roomId} has an open incident that blocks it. Resolve the incident before making the room available.");

/// <summary>A guest is staying in the room: it cannot be cleaned for a new stay until it is vacated.</summary>
public class RoomOccupiedException(int roomId)
    : BusinessRuleViolationException(AccommodationErrorCodes.RoomOccupied,
        $"Room {roomId} is occupied. It can be cleaned once the guest checks out.");
