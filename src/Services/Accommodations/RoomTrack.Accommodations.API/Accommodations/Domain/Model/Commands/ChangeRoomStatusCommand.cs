using BackendAwRoomTrack.API.Accommodations.Domain.Model.ValueObjects;

namespace BackendAwRoomTrack.API.Accommodations.Domain.Model.Commands;

/// <summary>A staff member changes the operational status of a room.</summary>
/// <param name="RoomId">The room.</param>
/// <param name="Status">The new status.</param>
/// <param name="ChangedByUserId">Who changes it.</param>
/// <param name="ChangedByEmail">Their e-mail (kept in the history).</param>
public record ChangeRoomStatusCommand(int RoomId, RoomStatus Status, int ChangedByUserId, string? ChangedByEmail);

/// <summary>The guest of a completed check-in moves into the room (it becomes Occupied).</summary>
public record OccupyRoomForCheckInCommand(int RoomId, int? GuestUserId, string? GuestEmail);

/// <summary>Alert the administrators of rooms under maintenance for too long. Run by the scheduler.</summary>
public record RaiseMaintenanceAlertsCommand;

/// <summary>A blocking incident takes the room out of service (Maintenance) until it is released.</summary>
public record HoldRoomForIncidentCommand(int RoomId, int? ReportedByUserId, string? ReportedByEmail);

/// <summary>The last blocking incident of the room was resolved: the room goes to Cleaning.</summary>
public record ReleaseRoomFromIncidentCommand(int RoomId, int? ResolvedByUserId, string? ResolvedByEmail);
