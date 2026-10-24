using BackendAwRoomTrack.API.Accommodations.Domain.Model.Queries;
using BackendAwRoomTrack.API.Accommodations.Domain.Services;
using BackendAwRoomTrack.API.Housekeeping.Application.OutboundServices;
using BackendAwRoomTrack.API.Housekeeping.Domain.Model.Events;
using BackendAwRoomTrack.API.Housekeeping.Domain.Repositories;
using BackendAwRoomTrack.API.IAM.Domain.Model.Constants;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;
using BackendAwRoomTrack.API.Shared.Application.Internal.EventHandlers;

namespace BackendAwRoomTrack.API.Housekeeping.Application.Internal.EventHandlers;

/// <summary>
///     Notifies the hotel staff of what happens to a cleaning task. Recipients come from IAM through its ACL facade;
///     the staff member who made the change is never notified of it.
/// </summary>
/// <remarks>
///     Assigned → the new assignee (and the previous one, when it was reassigned); completed → reception (the room
///     can be assigned to a guest).
/// </remarks>
public class CleaningTaskNotificationHandler(
    ICleaningTaskRepository cleaningTaskRepository,
    IRoomQueryService roomQueryService,
    IIamContextFacade iamContextFacade,
    ICleaningTaskNotificationService notifications) :
    IDomainEventHandler<CleaningTaskAssignedEvent>,
    IDomainEventHandler<CleaningTaskCompletedEvent>
{
    public async Task HandleAsync(CleaningTaskAssignedEvent e, CancellationToken cancellationToken)
    {
        var notice = await NoticeAsync(e.CleaningTaskId, e.RoomId, e.HotelId);
        var assignedBy = e.AssignedByUserId is { } actorId ? await NameOfAsync(actorId) : null;

        if (e.AssigneeUserId != e.AssignedByUserId && await iamContextFacade.FetchUserContactAsync(e.AssigneeUserId) is { } assignee)
            await notifications.SendAssignedAsync(assignee, notice, assignedBy);
        if (e.PreviousAssigneeUserId is { } previousId && previousId != e.AssignedByUserId
            && await iamContextFacade.FetchUserContactAsync(previousId) is { } previous)
            await notifications.SendUnassignedAsync(previous, notice, assignedBy);
    }

    public async Task HandleAsync(CleaningTaskCompletedEvent e, CancellationToken cancellationToken)
    {
        var recipients = (await iamContextFacade.ListHotelStaffAsync(e.HotelId, [UserRoles.Reception]))
            .Where(contact => contact.UserId != e.CompletedByUserId).ToList();
        if (recipients.Count == 0) return;
        await notifications.SendRoomReadyAsync(recipients, await NoticeAsync(e.CleaningTaskId, e.RoomId, e.HotelId),
            await NameOfAsync(e.CompletedByUserId));
    }

    private async Task<string?> NameOfAsync(int userId) =>
        await iamContextFacade.FetchUserContactAsync(userId) is { } contact ? contact.FullName ?? contact.Email : null;

    private async Task<CleaningTaskNotice> NoticeAsync(int taskId, int roomId, int hotelId)
    {
        var task = await cleaningTaskRepository.FindByIdAsync(taskId)
                   ?? throw new InvalidOperationException($"Cleaning task {taskId} of a domain event was not found.");
        var room = await roomQueryService.Handle(new GetRoomByIdQuery(roomId));
        return new CleaningTaskNotice(task.Id, task.Priority, task.Notes, room?.Number ?? roomId.ToString(),
            room?.Hotel?.Name ?? $"hotel {hotelId}");
    }
}
