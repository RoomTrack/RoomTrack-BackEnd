using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Entities;
using RoomTrack.Housekeeping.Interfaces.REST.Resources;

namespace RoomTrack.Housekeeping.Interfaces.REST.Transform;

public static class CleaningTaskResourceAssembler
{
    /// <param name="task">The task.</param>
    /// <param name="withHistory">Include its history and impediments (loaded when a single task is read).</param>
    public static CleaningTaskResource ToResource(CleaningTask task, bool withHistory = true) =>
        new(task.Id, task.HotelId, task.RoomId, task.Priority.ToString(), task.Status.ToString(), task.Notes,
            task.CreatedByUserId, task.CreatedAt, task.AssigneeUserId, task.AssignedAt, task.AssignedAutomatically,
            task.StartedAt, task.CompletedAt, task.CompletedByUserId, task.CancelledAt, task.CancellationReason,
            withHistory ? task.ImpedimentIncidentIds : null,
            withHistory ? task.History.OrderBy(entry => entry.OccurredAt).ThenBy(entry => entry.Id).Select(ToResource).ToList() : null);

    public static HousekeepingSettingsResource ToResource(HotelHousekeepingSettings settings) =>
        new(settings.HotelId, settings.AutoAssign, settings.UpdatedAt, settings.UpdatedByUserId);

    private static CleaningTaskHistoryEntryResource ToResource(CleaningTaskHistoryEntry entry) =>
        new(entry.Action.ToString(), entry.Status.ToString(), entry.Detail, entry.IncidentId, entry.ActorUserId, entry.ActorEmail,
            entry.OccurredAt);
}
