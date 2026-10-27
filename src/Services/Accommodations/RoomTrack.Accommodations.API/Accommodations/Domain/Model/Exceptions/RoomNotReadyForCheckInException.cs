using RoomTrack.Accommodations.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Accommodations.Domain.Model.Exceptions;

/// <summary>A guest can only move into an Available room (not being cleaned, repaired or occupied).</summary>
public class RoomNotReadyForCheckInException(int roomId, RoomStatus status)
    : BusinessRuleViolationException(AccommodationErrorCodes.RoomNotReady,
        $"Room {roomId} is not ready yet ({status}). Try again in a few minutes or request assistance from the front desk.");
