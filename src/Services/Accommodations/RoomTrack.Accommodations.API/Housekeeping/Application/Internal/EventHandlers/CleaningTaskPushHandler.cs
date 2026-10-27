using System.Globalization;
using RoomTrack.Accommodations.Domain.Model.Queries;
using RoomTrack.Accommodations.Domain.Services;
using RoomTrack.Housekeeping.Domain.Model.Events;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Shared.Application.Internal.EventHandlers;
using RoomTrack.Shared.Application.OutboundServices;
using RoomTrack.Contracts.Messaging;

namespace RoomTrack.Housekeeping.Application.Internal.EventHandlers;

/// <summary>
///     Push notifications of the cleaning tasks to the housekeeping staff (US-22), next to their e-mails: a task
///     assigned to them or taken from them, and a change of urgency of their task (highlighted when it became urgent).
///     The staff member who made the change is never notified of it.
/// </summary>
public class CleaningTaskPushHandler(IRoomQueryService roomQueryService, IPushSender pushSender) :
    IDomainEventHandler<CleaningTaskAssignedEvent>,
    IDomainEventHandler<CleaningTaskPriorityChangedEvent>
{
    public async Task HandleAsync(CleaningTaskAssignedEvent e, CancellationToken cancellationToken)
    {
        var room = await RoomNumberAsync(e.RoomId);
        if (e.AssigneeUserId != e.AssignedByUserId)
            await pushSender.SendAsync(new PushMessage([e.AssigneeUserId], PushCategories.CleaningTaskAssigned,
                "Nueva limpieza asignada", $"Habitación {room}: ya está en tu lista de tareas.", Data(e.CleaningTaskId)),
                cancellationToken);
        if (e.PreviousAssigneeUserId is { } previous && previous != e.AssignedByUserId)
            await pushSender.SendAsync(new PushMessage([previous], PushCategories.CleaningTaskAssigned,
                "Limpieza reasignada", $"Habitación {room}: se asignó a otra persona.", Data(e.CleaningTaskId)),
                cancellationToken);
    }

    public async Task HandleAsync(CleaningTaskPriorityChangedEvent e, CancellationToken cancellationToken)
    {
        if (e.AssigneeUserId is not { } assignee || assignee == e.ChangedByUserId) return;
        var urgent = e.ToPriority == CleaningTaskPriority.Urgent;
        await pushSender.SendAsync(new PushMessage([assignee], PushCategories.PriorityChanged,
            urgent ? "¡Limpieza urgente!" : "Cambió la prioridad de una limpieza",
            $"Habitación {await RoomNumberAsync(e.RoomId)}: prioridad {Label(e.FromPriority)} → {Label(e.ToPriority)}.",
            Data(e.CleaningTaskId), urgent), cancellationToken);
    }

    private async Task<string> RoomNumberAsync(int roomId) =>
        (await roomQueryService.Handle(new GetRoomByIdQuery(roomId)))?.Number ?? roomId.ToString(CultureInfo.InvariantCulture);

    private static Dictionary<string, string> Data(int taskId) =>
        new() { ["screen"] = "cleaning-task", ["id"] = taskId.ToString(CultureInfo.InvariantCulture) };

    private static string Label(CleaningTaskPriority priority) => priority switch
    {
        CleaningTaskPriority.Low => "baja",
        CleaningTaskPriority.Normal => "normal",
        CleaningTaskPriority.High => "alta",
        _ => "urgente"
    };
}
