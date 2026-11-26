using RoomTrack.ShiftHandovers.Domain.Model.ValueObjects;

namespace RoomTrack.ShiftHandovers.Domain.Model.Commands;

/// <summary>
///     A staff member ends their shift (US-32): the open tasks and incidents of their area are recorded with their
///     notes. <paramref name="Area"/> is required for an administrator; staff hand over the area of their role.
/// </summary>
public record CreateShiftHandoverCommand(int HotelId, ShiftArea? Area, string? Notes, ShiftHandoverActor Author);

/// <summary>A colleague confirms reading a handover.</summary>
public record AcknowledgeShiftHandoverCommand(int ShiftHandoverId, ShiftHandoverActor Reader);
