using BackendAwRoomTrack.API.Housekeeping.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Incidents.Domain.Model.ValueObjects;

namespace BackendAwRoomTrack.API.Housekeeping.Domain.Model.Commands;

/// <summary>
///     The front desk creates the cleaning task of a room (US-05). Without <paramref name="AssigneeUserId"/> it is
///     assigned automatically when the hotel has the automatic assignment on, and left unassigned otherwise.
/// </summary>
public record CreateCleaningTaskCommand(int RoomId, CleaningTaskPriority Priority, string? Notes, int? AssigneeUserId,
    CleaningTaskActor Actor);

/// <summary>
///     Assigns (or reassigns) a cleaning task. Without <paramref name="AssigneeUserId"/> the least loaded housekeeping
///     staff member of the hotel is chosen.
/// </summary>
public record AssignCleaningTaskCommand(int CleaningTaskId, int? AssigneeUserId, CleaningTaskActor Actor);

/// <summary>The assignee starts cleaning: the room goes to Cleaning. A housekeeper starting an unassigned task takes it.</summary>
public record StartCleaningTaskCommand(int CleaningTaskId, CleaningTaskActor Actor);

/// <summary>The assignee finished cleaning: the room becomes Available.</summary>
public record CompleteCleaningTaskCommand(int CleaningTaskId, CleaningTaskActor Actor);

/// <summary>
///     The assignee found a damage while cleaning: an incident is reported for maintenance and linked to the task
///     (US-06).
/// </summary>
public record ReportCleaningImpedimentCommand(int CleaningTaskId, string Title, string? Description, IncidentPriority Priority,
    bool BlocksRoom, CleaningTaskActor Actor);

/// <summary>Re-classifies the urgency of a cleaning task.</summary>
public record ChangeCleaningTaskPriorityCommand(int CleaningTaskId, CleaningTaskPriority Priority, CleaningTaskActor Actor);

/// <summary>The front desk cancels an open cleaning task.</summary>
public record CancelCleaningTaskCommand(int CleaningTaskId, string? Reason, CleaningTaskActor Actor);

/// <summary>Turns the automatic assignment of the cleaning tasks of a hotel on or off.</summary>
public record ConfigureHousekeepingCommand(int HotelId, bool AutoAssign, int UpdatedByUserId);
