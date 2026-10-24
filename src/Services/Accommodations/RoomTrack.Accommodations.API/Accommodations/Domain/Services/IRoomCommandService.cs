using BackendAwRoomTrack.API.Accommodations.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Commands;

namespace BackendAwRoomTrack.API.Accommodations.Domain.Services;

/// <summary>
/// Defines the contract for services that handle room state changes.
/// </summary>
public interface IRoomCommandService
{
    Task<Room?> Handle(CreateRoomCommand command);
    
    /// <summary>
    /// Handles the update of a room.
    /// </summary>
    /// <param name="command">The update command.</param>
    /// <returns>The updated room or null if not found.</returns>
    Task<Room?> Handle(UpdateRoomCommand command);

    /// <summary>
    /// Handles the deletion of a room.
    /// </summary>
    /// <param name="command">The delete command.</param>
    /// <returns>The deleted room or null if not found.</returns>
    Task<Room?> Handle(DeleteRoomCommand command);

    /// <summary>Changes the room status. Null when the room does not exist.</summary>
    Task<Room?> Handle(ChangeRoomStatusCommand command);

    /// <summary>The room of a completed check-in becomes Occupied (must be Available).</summary>
    Task<Room> Handle(OccupyRoomForCheckInCommand command);

    /// <summary>A blocking incident takes the room out of service. Runs in the caller's unit of work.</summary>
    Task<Room> Handle(HoldRoomForIncidentCommand command);

    /// <summary>The room is no longer blocked by incidents and goes to cleaning. Runs in the caller's unit of work.</summary>
    Task<Room> Handle(ReleaseRoomFromIncidentCommand command);

    /// <summary>Housekeeping starts cleaning the room. Runs in the caller's unit of work.</summary>
    Task<Room> Handle(BeginRoomCleaningCommand command);

    /// <summary>Housekeeping finished cleaning the room: it becomes Available. Runs in the caller's unit of work.</summary>
    Task<Room> Handle(FinishRoomCleaningCommand command);

    /// <returns>How many rooms were alerted.</returns>
    Task<int> Handle(RaiseMaintenanceAlertsCommand command);
}