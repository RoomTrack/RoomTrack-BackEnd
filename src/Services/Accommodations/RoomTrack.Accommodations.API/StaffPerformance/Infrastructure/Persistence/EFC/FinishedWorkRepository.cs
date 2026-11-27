using Microsoft.EntityFrameworkCore;
using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using RoomTrack.StaffPerformance.Domain.Model.ValueObjects;
using RoomTrack.StaffPerformance.Domain.Repositories;

namespace RoomTrack.StaffPerformance.Infrastructure.Persistence.EFC;

/// <summary>
///     Projects the finished cleaning tasks, incidents and guest requests of the Accommodations database. No table of
///     its own: the reports are calculated from the timestamps the operational contexts already record.
/// </summary>
public class FinishedWorkRepository(AppDbContext context) : IFinishedWorkRepository
{
    public async Task<IReadOnlyList<FinishedWork>> ListFinishedAsync(int hotelId, DateTimeOffset from, DateTimeOffset to)
    {
        var cleaning = await context.Set<CleaningTask>().AsNoTracking()
            .Where(task => task.HotelId == hotelId && task.Status == CleaningTaskStatus.Completed
                           && task.CompletedAt >= from && task.CompletedAt < to)
            .Select(task => new
            {
                task.Id, UserId = task.CompletedByUserId ?? task.AssigneeUserId, task.CreatedAt, task.StartedAt,
                FinishedAt = task.CompletedAt!.Value
            })
            .ToListAsync();

        var incidents = await context.Set<Incident>().AsNoTracking()
            .Where(incident => incident.HotelId == hotelId && incident.Status == IncidentStatus.Resolved
                               && incident.ResolvedAt >= from && incident.ResolvedAt < to)
            .Select(incident => new
            {
                incident.Id, UserId = incident.ResolvedByUserId ?? incident.AssigneeUserId, incident.ReportedAt,
                incident.StartedAt, FinishedAt = incident.ResolvedAt!.Value, incident.Kind
            })
            .ToListAsync();

        var requests = await context.Set<GuestRequest>().AsNoTracking()
            .Where(request => request.HotelId == hotelId && request.Status == GuestRequestStatus.Resolved
                              && request.ResolvedAt >= from && request.ResolvedAt < to)
            .Select(request => new
            {
                request.Id, request.AssigneeUserId, request.Area, request.CreatedAt, request.StartedAt,
                FinishedAt = request.ResolvedAt!.Value
            })
            .ToListAsync();

        return cleaning
            .Select(task => new FinishedWork(WorkKind.CleaningTask, task.Id, WorkArea.Housekeeping, task.UserId,
                task.CreatedAt, task.StartedAt, task.FinishedAt))
            .Concat(incidents.Select(incident => new FinishedWork(WorkKind.Incident, incident.Id, WorkArea.Maintenance,
                incident.UserId, incident.ReportedAt, incident.StartedAt, incident.FinishedAt,
                CountsForServiceTimes: incident.Kind != IncidentKind.Preventive)))
            .Concat(requests.Select(request => new FinishedWork(WorkKind.GuestRequest, request.Id, AreaOf(request.Area),
                request.AssigneeUserId, request.CreatedAt, request.StartedAt, request.FinishedAt)))
            .ToList();
    }

    private static WorkArea AreaOf(ServiceArea area) => area switch
    {
        ServiceArea.Housekeeping => WorkArea.Housekeeping,
        ServiceArea.Maintenance => WorkArea.Maintenance,
        _ => WorkArea.FrontDesk
    };
}
