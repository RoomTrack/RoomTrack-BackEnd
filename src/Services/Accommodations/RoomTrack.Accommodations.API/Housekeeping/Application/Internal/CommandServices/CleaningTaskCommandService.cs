using BackendAwRoomTrack.API.Accommodations.Domain.Model.Commands;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Queries;
using BackendAwRoomTrack.API.Accommodations.Domain.Services;
using BackendAwRoomTrack.API.Housekeeping.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Housekeeping.Domain.Model.Commands;
using BackendAwRoomTrack.API.Housekeeping.Domain.Model.Exceptions;
using BackendAwRoomTrack.API.Housekeeping.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Housekeeping.Domain.Repositories;
using BackendAwRoomTrack.API.Housekeeping.Domain.Services;
using BackendAwRoomTrack.API.IAM.Domain.Model.Constants;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;
using BackendAwRoomTrack.API.Incidents.Domain.Model.Commands;
using BackendAwRoomTrack.API.Incidents.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Incidents.Domain.Services;
using BackendAwRoomTrack.API.Shared.Domain.Repositories;
using BackendAwRoomTrack.Domain.Shared.Domain.Model.Exceptions;

namespace BackendAwRoomTrack.API.Housekeeping.Application.Internal.CommandServices;

/// <summary>
///     Use cases of the cleaning tasks. The room of a task is changed through the Rooms context
///     (<see cref="IRoomCommandService"/>) and its impediments are reported through the Incidents context
///     (<see cref="IIncidentCommandService"/>), inside the same transaction as the task change, so a task and the
///     status of its room never disagree.
/// </summary>
public class CleaningTaskCommandService(
    ICleaningTaskRepository cleaningTaskRepository,
    IHotelHousekeepingSettingsRepository settingsRepository,
    ICleaningTaskAssignmentStrategy assignmentStrategy,
    IRoomQueryService roomQueryService,
    IRoomCommandService roomCommandService,
    IIncidentCommandService incidentCommandService,
    IIamContextFacade iamContextFacade,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICleaningTaskCommandService
{
    public async Task<CleaningTask> Handle(CreateCleaningTaskCommand command)
    {
        var room = await roomQueryService.Handle(new GetRoomByIdQuery(command.RoomId))
                   ?? throw new EntityNotFoundException("Room", command.RoomId);
        if (await cleaningTaskRepository.FindOpenByRoomAsync(room.Id) is { } open)
            throw new RoomAlreadyHasOpenCleaningTaskException(room.Id, open.Id);

        var now = timeProvider.GetUtcNow();
        var task = CleaningTask.Create(room.HotelId, room.Id, command.Priority, command.Notes, command.Actor, now);
        if (command.AssigneeUserId is { } assigneeUserId)
        {
            await EnsureHousekeeperOfHotelAsync(task.HotelId, assigneeUserId);
            task.AssignTo(assigneeUserId, command.Actor, now);
        }
        else if ((await settingsRepository.FindByHotelIdAsync(task.HotelId))?.AutoAssign == true
                 && await assignmentStrategy.SelectAssigneeAsync(task.HotelId) is { } selected)
        {
            // Without housekeeping staff the task simply waits unassigned.
            task.AssignAutomatically(selected, command.Actor, now);
        }

        // The assignment event needs the generated id: saved first, then published in the same transaction.
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await cleaningTaskRepository.AddAsync(task);
            await unitOfWork.CompleteAsync();
        });
        return task;
    }

    public async Task<CleaningTask?> Handle(AssignCleaningTaskCommand command)
    {
        var task = await cleaningTaskRepository.FindByIdAsync(command.CleaningTaskId);
        if (task is null) return null;

        var now = timeProvider.GetUtcNow();
        if (command.AssigneeUserId is { } assigneeUserId)
        {
            // A housekeeper taking it for themselves is already known to be one (endpoint policy and hotel scope).
            var takesItThemselves = assigneeUserId == command.Actor.UserId && !command.Actor.CanManage;
            if (!takesItThemselves) await EnsureHousekeeperOfHotelAsync(task.HotelId, assigneeUserId);
            task.AssignTo(assigneeUserId, command.Actor, now);
        }
        else
        {
            if (!command.Actor.CanManage) throw new CleaningTaskManageNotAllowedException("automatically assign");
            var selected = await assignmentStrategy.SelectAssigneeAsync(task.HotelId, excludingUserId: task.AssigneeUserId)
                           ?? throw new NoHousekeeperAvailableException(task.HotelId);
            task.AssignAutomatically(selected, command.Actor, now);
        }

        await unitOfWork.CompleteAsync();
        return task;
    }

    public async Task<CleaningTask?> Handle(StartCleaningTaskCommand command)
    {
        var task = await cleaningTaskRepository.FindByIdAsync(command.CleaningTaskId);
        if (task is null) return null;

        var now = timeProvider.GetUtcNow();
        if (task.AssigneeUserId is null && command.Actor.Role == CleaningTaskRole.Housekeeper)
            task.AssignTo(command.Actor.UserId, command.Actor, now);
        if (!task.Start(command.Actor, now)) return task;

        // The room goes to Cleaning with the task: "en limpieza" is visible to the whole hotel (US-06).
        await unitOfWork.ExecuteInTransactionAsync(() =>
            roomCommandService.Handle(new BeginRoomCleaningCommand(task.RoomId, command.Actor.UserId, command.Actor.Email)));
        return task;
    }

    public async Task<CleaningTask?> Handle(CompleteCleaningTaskCommand command)
    {
        var task = await cleaningTaskRepository.FindByIdAsync(command.CleaningTaskId);
        if (task is null) return null;

        if (!task.Complete(command.Actor, timeProvider.GetUtcNow())) return task;

        // The room becomes Available with the task, so reception can assign it to a new guest; a room blocked by an
        // incident refuses it and nothing is saved.
        await unitOfWork.ExecuteInTransactionAsync(() =>
            roomCommandService.Handle(new FinishRoomCleaningCommand(task.RoomId, command.Actor.UserId, command.Actor.Email)));
        return task;
    }

    public async Task<CleaningTask?> Handle(ReportCleaningImpedimentCommand command)
    {
        var task = await cleaningTaskRepository.FindByIdAsync(command.CleaningTaskId);
        if (task is null) return null;

        var reporter = new IncidentActor(command.Actor.UserId, command.Actor.Email, command.Actor.IsAdministrator);
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var incident = await incidentCommandService.Handle(new ReportIncidentCommand(task.RoomId, command.Title,
                command.Description, command.Priority, command.BlocksRoom, reporter));
            task.RecordImpediment(incident.Id, incident.Title, command.Actor, timeProvider.GetUtcNow());
            await unitOfWork.CompleteAsync();
        });
        return task;
    }

    public async Task<CleaningTask?> Handle(ChangeCleaningTaskPriorityCommand command)
    {
        var task = await cleaningTaskRepository.FindByIdAsync(command.CleaningTaskId);
        if (task is null) return null;

        task.ChangePriority(command.Priority, command.Actor, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return task;
    }

    public async Task<CleaningTask?> Handle(CancelCleaningTaskCommand command)
    {
        var task = await cleaningTaskRepository.FindByIdAsync(command.CleaningTaskId);
        if (task is null) return null;

        task.Cancel(command.Reason, command.Actor, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return task;
    }

    public async Task<HotelHousekeepingSettings> Handle(ConfigureHousekeepingCommand command)
    {
        var settings = await settingsRepository.FindByHotelIdAsync(command.HotelId);
        if (settings is null)
        {
            settings = new HotelHousekeepingSettings(command.HotelId);
            await settingsRepository.AddAsync(settings);
        }
        settings.Configure(command.AutoAssign, command.UpdatedByUserId, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return settings;
    }

    private async Task EnsureHousekeeperOfHotelAsync(int hotelId, int userId)
    {
        var housekeepers = await iamContextFacade.ListHotelStaffAsync(hotelId, [UserRoles.Housekeeping]);
        if (housekeepers.All(contact => contact.UserId != userId))
            throw new InvalidFieldException("assigneeUserId", CleaningTaskErrorCodes.AssigneeNotHousekeeping,
                $"User {userId} is not an active housekeeping staff member of hotel {hotelId}.");
    }
}
