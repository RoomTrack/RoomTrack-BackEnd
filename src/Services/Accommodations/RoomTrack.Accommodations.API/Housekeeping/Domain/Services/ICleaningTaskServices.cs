using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Commands;
using RoomTrack.Housekeeping.Domain.Model.Queries;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;

namespace RoomTrack.Housekeeping.Domain.Services;

public interface ICleaningTaskCommandService
{
    /// <exception cref="RoomTrack.Shared.Domain.Model.Exceptions.EntityNotFoundException">The room does not exist.</exception>
    Task<CleaningTask> Handle(CreateCleaningTaskCommand command);

    /// <returns>The task, or null when it does not exist.</returns>
    Task<CleaningTask?> Handle(AssignCleaningTaskCommand command);

    /// <summary>Starts the task; its room goes to Cleaning in the same transaction.</summary>
    /// <returns>The task, or null when it does not exist.</returns>
    Task<CleaningTask?> Handle(StartCleaningTaskCommand command);

    /// <summary>Completes the task; its room becomes Available in the same transaction.</summary>
    /// <returns>The task, or null when it does not exist.</returns>
    Task<CleaningTask?> Handle(CompleteCleaningTaskCommand command);

    /// <summary>Reports an incident of the task's room and links it to the task, in one transaction.</summary>
    /// <returns>The task, or null when it does not exist.</returns>
    Task<CleaningTask?> Handle(ReportCleaningImpedimentCommand command);

    /// <returns>The task, or null when it does not exist.</returns>
    Task<CleaningTask?> Handle(ChangeCleaningTaskPriorityCommand command);

    /// <returns>The task, or null when it does not exist.</returns>
    Task<CleaningTask?> Handle(CancelCleaningTaskCommand command);

    Task<HotelHousekeepingSettings> Handle(ConfigureHousekeepingCommand command);

    /// <summary>Marks a checklist step of the task (US-07).</summary>
    /// <returns>The task, or null when it does not exist.</returns>
    Task<CleaningTask?> Handle(MarkCleaningChecklistItemCommand command);

    /// <summary>Creates or replaces the cleaning protocol of the hotel for a room type (US-07).</summary>
    Task<CleaningChecklistTemplate> Handle(ConfigureCleaningChecklistCommand command);
}

public interface ICleaningTaskQueryService
{
    Task<CleaningTask?> Handle(GetCleaningTaskByIdQuery query);

    Task<IReadOnlyList<CleaningTask>> Handle(GetCleaningTasksQuery query);

    /// <summary>The settings of the hotel (defaults when it never configured them).</summary>
    Task<HotelHousekeepingSettings> Handle(GetHousekeepingSettingsQuery query);

    /// <summary>The protocols configured by the hotel (US-07).</summary>
    Task<IReadOnlyList<CleaningChecklistTemplate>> Handle(GetCleaningChecklistTemplatesQuery query);

    /// <summary>The steps a new task of a room of that type gets: its type's, the hotel default or the standard ones.</summary>
    Task<IReadOnlyList<ChecklistItemDefinition>> Handle(GetCleaningChecklistForRoomQuery query);
}

/// <summary>
///     Chooses who gets a cleaning task when nobody is named (Strategy pattern: the rest of the flow does not depend on
///     how the choice is made).
/// </summary>
public interface ICleaningTaskAssignmentStrategy
{
    /// <param name="hotelId">The hotel of the task.</param>
    /// <param name="excludingUserId">
    ///     The current assignee of a task being reassigned: someone else is chosen when the hotel has anyone else.
    /// </param>
    /// <returns>The IAM user id of the chosen housekeeping staff member, or null when the hotel has none.</returns>
    Task<int?> SelectAssigneeAsync(int hotelId, int? excludingUserId = null);
}
