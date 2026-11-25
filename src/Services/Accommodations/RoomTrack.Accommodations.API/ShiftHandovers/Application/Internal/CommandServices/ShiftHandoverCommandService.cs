using RoomTrack.Housekeeping.Domain.Model.Queries;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Housekeeping.Domain.Repositories;
using RoomTrack.Incidents.Domain.Model.Queries;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Incidents.Domain.Repositories;
using RoomTrack.Shared.Domain.Model.Exceptions;
using RoomTrack.Shared.Domain.Repositories;
using RoomTrack.ShiftHandovers.Domain.Model.Aggregates;
using RoomTrack.ShiftHandovers.Domain.Model.Commands;
using RoomTrack.ShiftHandovers.Domain.Model.Exceptions;
using RoomTrack.ShiftHandovers.Domain.Model.ValueObjects;
using RoomTrack.ShiftHandovers.Domain.Repositories;
using RoomTrack.ShiftHandovers.Domain.Services;

namespace RoomTrack.ShiftHandovers.Application.Internal.CommandServices;

/// <summary>
///     Use cases of the shift handovers (US-32). The open items are read from the Housekeeping and Incidents contexts of
///     the same service when the shift ends:
///     <list type="bullet">
///         <item>
///             Housekeeping: the open cleaning tasks of the author and the unassigned ones, and the open incidents the
///             author reported (damages found while cleaning).
///         </item>
///         <item>Maintenance: the open incidents of the author and the unassigned ones.</item>
///         <item>Front desk: the urgent cleaning tasks and the high or critical incidents of the hotel.</item>
///     </list>
///     An administrator handing over an area gets every open item of it.
/// </summary>
public class ShiftHandoverCommandService(
    IShiftHandoverRepository handoverRepository,
    ICleaningTaskRepository cleaningTaskRepository,
    IIncidentRepository incidentRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IShiftHandoverCommandService
{
    private const int MaxOpenItemsRead = 500;

    public async Task<ShiftHandover> Handle(CreateShiftHandoverCommand command)
    {
        var area = command.Author.Area ?? command.Area
                   ?? throw new InvalidFieldException("area", ShiftHandoverErrorCodes.AreaRequired,
                       "Name the area of the handover: FrontDesk, Housekeeping or Maintenance.");
        var mineOnly = command.Author.Area is not null;
        var items = await OpenItemsAsync(command.HotelId, area, command.Author.UserId, mineOnly);

        var handover = ShiftHandover.Create(command.HotelId, area, command.Author, command.Notes, items, timeProvider.GetUtcNow());
        await handoverRepository.AddAsync(handover);
        await unitOfWork.CompleteAsync();
        return handover;
    }

    public async Task<ShiftHandover?> Handle(AcknowledgeShiftHandoverCommand command)
    {
        var handover = await handoverRepository.FindByIdAsync(command.ShiftHandoverId);
        if (handover is null) return null;
        handover.Acknowledge(command.Reader, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return handover;
    }

    private async Task<IReadOnlyList<ShiftHandoverItemSnapshot>> OpenItemsAsync(int hotelId, ShiftArea area, int authorUserId,
        bool mineOnly)
    {
        var items = new List<ShiftHandoverItemSnapshot>();
        if (area is ShiftArea.Housekeeping or ShiftArea.FrontDesk)
        {
            var tasks = await cleaningTaskRepository.ListAsync(new GetCleaningTasksQuery(hotelId, OnlyOpen: true, Limit: MaxOpenItemsRead));
            items.AddRange(tasks
                .Where(task => area == ShiftArea.Housekeeping
                    ? !mineOnly || task.AssigneeUserId is null || task.AssigneeUserId == authorUserId
                    : task.Priority is CleaningTaskPriority.Urgent or CleaningTaskPriority.High)
                .Select(task => new ShiftHandoverItemSnapshot(ShiftHandoverItemKind.CleaningTask, task.Id, task.RoomId,
                    task.Notes ?? $"Limpieza de la habitación {task.RoomId}", task.Status.ToString(), task.Priority.ToString(),
                    task.AssigneeUserId)));
        }
        if (area is ShiftArea.Maintenance or ShiftArea.FrontDesk || (area == ShiftArea.Housekeeping && mineOnly))
        {
            var incidents = await incidentRepository.ListAsync(new GetIncidentsQuery(hotelId, OnlyOpen: true, Limit: MaxOpenItemsRead));
            items.AddRange(incidents
                .Where(incident => area switch
                {
                    ShiftArea.Maintenance => !mineOnly || incident.AssigneeUserId is null || incident.AssigneeUserId == authorUserId,
                    ShiftArea.Housekeeping => incident.ReportedByUserId == authorUserId,
                    _ => incident.Priority is IncidentPriority.High or IncidentPriority.Critical
                })
                .Select(incident => new ShiftHandoverItemSnapshot(ShiftHandoverItemKind.Incident, incident.Id, incident.RoomId,
                    incident.Title, incident.Status.ToString(), incident.Priority.ToString(), incident.AssigneeUserId)));
        }
        return items;
    }
}
