using RoomTrack.Incidents.Domain.Model.Aggregates;
using RoomTrack.Incidents.Domain.Model.Queries;
using RoomTrack.Incidents.Domain.Model.ValueObjects;
using RoomTrack.Incidents.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Incidents.Infrastructure.Persistence.EFC.Repositories;

public class IncidentRepository(AppDbContext context) : IIncidentRepository
{
    public async Task AddAsync(Incident incident) => await context.Set<Incident>().AddAsync(incident);

    public Task<Incident?> FindByIdAsync(int id) =>
        context.Set<Incident>().Include(i => i.History).FirstOrDefaultAsync(i => i.Id == id);

    public async Task<IReadOnlyList<Incident>> ListAsync(GetIncidentsQuery query)
    {
        var incidents = context.Set<Incident>().Where(i => i.HotelId == query.HotelId);
        if (query.Status is { } status) incidents = incidents.Where(i => i.Status == status);
        if (query.OnlyOpen) incidents = incidents.Where(i => i.Status != IncidentStatus.Resolved);
        if (query.Priority is { } priority) incidents = incidents.Where(i => i.Priority == priority);
        if (query.RoomId is { } roomId) incidents = incidents.Where(i => i.RoomId == roomId);
        if (query.AssigneeUserId is { } assigneeUserId) incidents = incidents.Where(i => i.AssigneeUserId == assigneeUserId);

        // Priorities are stored by name: ranked explicitly so Critical comes first, then the oldest report.
        return await incidents
            .OrderByDescending(i => i.Priority == IncidentPriority.Critical ? 3
                : i.Priority == IncidentPriority.High ? 2
                : i.Priority == IncidentPriority.Medium ? 1 : 0)
            .ThenBy(i => i.ReportedAt).ThenBy(i => i.Id)
            .Take(query.Limit)
            .ToListAsync();
    }

    public Task<bool> AnyOpenBlockingAsync(int roomId, int excludingIncidentId) =>
        context.Set<Incident>().AnyAsync(i => i.RoomId == roomId && i.Id != excludingIncidentId && i.BlocksRoom
                                              && i.Status != IncidentStatus.Resolved);

    public async Task<IReadOnlyList<Incident>> ListEscalationCandidatesAsync() =>
        await context.Set<Incident>().Include(i => i.History)
            .Where(i => i.Priority == IncidentPriority.Critical && i.Status == IncidentStatus.Pending && i.EscalatedAt == null)
            .ToListAsync();
}
