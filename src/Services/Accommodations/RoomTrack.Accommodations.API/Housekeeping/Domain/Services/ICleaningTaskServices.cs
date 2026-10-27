using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Commands;
using RoomTrack.Housekeeping.Domain.Model.Queries;

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
}

public interface ICleaningTaskQueryService
{
    Task<CleaningTask?> Handle(GetCleaningTaskByIdQuery query);

    Task<IReadOnlyList<CleaningTask>> Handle(GetCleaningTasksQuery query);

    /// <summary>The settings of the hotel (defaults when it never configured them).</summary>
    Task<HotelHousekeepingSettings> Handle(GetHousekeepingSettingsQuery query);
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
