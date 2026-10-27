using RoomTrack.Housekeeping.Domain.Model.ValueObjects;

namespace RoomTrack.Housekeeping.Domain.Model.Queries;

/// <summary>A cleaning task with its history.</summary>
public record GetCleaningTaskByIdQuery(int CleaningTaskId);

/// <summary>
///     The cleaning tasks of a hotel, most urgent first (priority, then oldest). Every filter is optional.
/// </summary>
public record GetCleaningTasksQuery(
    int HotelId,
    CleaningTaskStatus? Status = null,
    CleaningTaskPriority? Priority = null,
    int? RoomId = null,
    int? AssigneeUserId = null,
    bool OnlyOpen = false,
    int Limit = 100);

/// <summary>How the hotel distributes its cleaning tasks.</summary>
public record GetHousekeepingSettingsQuery(int HotelId);
