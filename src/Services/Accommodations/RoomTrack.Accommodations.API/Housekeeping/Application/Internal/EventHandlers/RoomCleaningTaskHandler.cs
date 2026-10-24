using BackendAwRoomTrack.API.Accommodations.Domain.Model.Events;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Housekeeping.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Housekeeping.Domain.Repositories;
using BackendAwRoomTrack.API.Housekeeping.Domain.Services;
using BackendAwRoomTrack.API.Shared.Application.Internal.EventHandlers;

namespace BackendAwRoomTrack.API.Housekeeping.Application.Internal.EventHandlers;

/// <summary>
///     Keeps the cleaning tasks in step with the rooms changed outside Housekeeping (runs inside the transaction of
///     the room change; the unit of work saves what it adds).
/// </summary>
/// <remarks>
///     A room that goes to <b>Cleaning</b> (a staff change, a resolved blocking incident) gets a task when it has no
///     open one, assigned automatically when the hotel has the automatic assignment on: this is the "new task" of
///     US-05. A room a staff member makes <b>Available</b> directly no longer needs its open task, which is
///     cancelled. Changes made by Housekeeping itself (starting or completing a task) are ignored.
/// </remarks>
public class RoomCleaningTaskHandler(
    ICleaningTaskRepository cleaningTaskRepository,
    IHotelHousekeepingSettingsRepository settingsRepository,
    ICleaningTaskAssignmentStrategy assignmentStrategy,
    TimeProvider timeProvider) : IDomainEventHandler<RoomStatusChangedEvent>
{
    public async Task HandleAsync(RoomStatusChangedEvent e, CancellationToken cancellationToken)
    {
        if (e.Origin == RoomStatusChangeOrigin.Housekeeping) return;

        switch (e.ToStatus)
        {
            case RoomStatus.Cleaning:
                await CreateTaskAsync(e);
                break;
            case RoomStatus.Available when e.Origin == RoomStatusChangeOrigin.Staff:
                if (await cleaningTaskRepository.FindOpenByRoomAsync(e.RoomId) is { } open)
                    open.Cancel("The room was made available without completing its cleaning task.", null, timeProvider.GetUtcNow());
                break;
        }
    }

    private async Task CreateTaskAsync(RoomStatusChangedEvent e)
    {
        if (await cleaningTaskRepository.FindOpenByRoomAsync(e.RoomId) is not null) return;

        var now = timeProvider.GetUtcNow();
        var task = CleaningTask.CreateForRoomToClean(e.HotelId, e.RoomId, now);
        if ((await settingsRepository.FindByHotelIdAsync(e.HotelId))?.AutoAssign == true
            && await assignmentStrategy.SelectAssigneeAsync(e.HotelId) is { } assigneeUserId)
            task.AssignAutomatically(assigneeUserId, null, now);
        await cleaningTaskRepository.AddAsync(task);
    }
}
