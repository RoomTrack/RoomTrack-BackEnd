using Microsoft.EntityFrameworkCore;
using RoomTrack.Accommodations.Domain.Model.Aggregates;
using RoomTrack.Dashboard.Domain.Model;
using RoomTrack.GuestRequests.Domain.Model.Aggregates;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;

namespace RoomTrack.Dashboard.Infrastructure;

/// <summary>Reads the operational state of a hotel for the administrator's panel (read only; no table of its own).</summary>
public interface IHotelOperationsRepository
{
    Task<HotelOperations> ReadAsync(int hotelId);
}

public class HotelOperationsRepository(AppDbContext context) : IHotelOperationsRepository
{
    public async Task<HotelOperations> ReadAsync(int hotelId)
    {
        var rooms = await context.Set<Room>().AsNoTracking()
            .Where(room => room.HotelId == hotelId)
            .Select(room => new { room.Id, room.Number, room.Status })
            .ToListAsync();

        var tasks = await context.Set<CleaningTask>().AsNoTracking()
            .Where(task => task.HotelId == hotelId
                           && (task.Status == CleaningTaskStatus.Pending || task.Status == CleaningTaskStatus.InProgress))
            .Select(task => new { task.Id, task.RoomId, task.Priority, task.AssigneeUserId, task.CreatedAt })
            .ToListAsync();

        var incidents = await context.Set<Incident>().AsNoTracking()
            .Where(incident => incident.HotelId == hotelId && incident.Status != IncidentStatus.Resolved)
            .Select(incident => new
            {
                incident.Id, incident.RoomId, incident.Title, incident.Priority, incident.BlocksRoom, incident.ReportedAt,
                incident.EscalatedAt, incident.IsRecurring
            })
            .ToListAsync();

        var requests = await context.Set<GuestRequest>().AsNoTracking()
            .Where(request => request.HotelId == hotelId
                              && (request.Status == GuestRequestStatus.Pending || request.Status == GuestRequestStatus.InProgress))
            .Select(request => new
            {
                request.Id, request.RoomId, request.Title, request.Area, request.Status, request.CreatedAt,
                request.EstimatedAttentionBy
            })
            .ToListAsync();

        return new HotelOperations(
            rooms.Select(room => new RoomSnapshot(room.Id, room.Number, room.Status.ToString())).ToList(),
            tasks.Select(task => new OpenCleaningTask(task.Id, task.RoomId, task.Priority.ToString(), task.AssigneeUserId,
                task.CreatedAt)).ToList(),
            incidents.Select(incident => new OpenIncident(incident.Id, incident.RoomId, incident.Title,
                incident.Priority.ToString(), incident.BlocksRoom, incident.ReportedAt, incident.EscalatedAt,
                incident.IsRecurring)).ToList(),
            requests.Select(request => new OpenGuestRequest(request.Id, request.RoomId, request.Title, request.Area.ToString(),
                request.Status.ToString(), request.CreatedAt, request.EstimatedAttentionBy)).ToList());
    }
}
