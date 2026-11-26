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

    public async Task<int> CountSimilarSinceAsync(int roomId, string title, DateTimeOffset since)
    {
        var normalized = title.Trim().ToLower();
        var stored = await context.Set<Incident>()
            .CountAsync(i => i.RoomId == roomId && i.ReportedAt >= since && i.Title.ToLower() == normalized);
        var added = context.ChangeTracker.Entries<Incident>()
            .Count(entry => entry.State == EntityState.Added && entry.Entity.RoomId == roomId
                            && entry.Entity.Title.Trim().ToLower() == normalized);
        return stored + added;
    }

    public async Task<IReadOnlyList<Incident>> ListAlertedAsync(int hotelId, DateTimeOffset from, DateTimeOffset to, int limit) =>
        await context.Set<Incident>().Include(i => i.History)
            .Where(i => i.HotelId == hotelId
                        && ((i.EscalatedAt != null && i.EscalatedAt >= from && i.EscalatedAt <= to)
                            || (i.IsRecurring && i.ReportedAt >= from && i.ReportedAt <= to)))
            .OrderByDescending(i => i.ReportedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<IReadOnlyList<Incident>> ListByRoomAsync(int roomId, int limit) =>
        await context.Set<Incident>().Where(i => i.RoomId == roomId)
            .OrderByDescending(i => i.ReportedAt).ThenByDescending(i => i.Id)
            .Take(limit)
            .ToListAsync();

    public async Task<IReadOnlyList<Incident>> ListEscalationCandidatesAsync() =>
        await context.Set<Incident>().Include(i => i.History)
            .Where(i => i.Priority == IncidentPriority.Critical && i.Status == IncidentStatus.Pending && i.EscalatedAt == null)
            .ToListAsync();
}

public class PreventiveMaintenancePlanRepository(AppDbContext context) : IPreventiveMaintenancePlanRepository
{
    public async Task AddAsync(PreventiveMaintenancePlan plan) => await context.Set<PreventiveMaintenancePlan>().AddAsync(plan);

    public Task<PreventiveMaintenancePlan?> FindByIdAsync(int id) =>
        context.Set<PreventiveMaintenancePlan>().FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IReadOnlyList<PreventiveMaintenancePlan>> ListAsync(int hotelId, int? roomId, bool onlyActive)
    {
        var plans = context.Set<PreventiveMaintenancePlan>().Where(p => p.HotelId == hotelId);
        if (roomId is { } room) plans = plans.Where(p => p.RoomId == room);
        if (onlyActive) plans = plans.Where(p => p.IsActive);
        return await plans.OrderByDescending(p => p.IsActive).ThenBy(p => p.NextDueOn).ThenBy(p => p.Id).ToListAsync();
    }

    public async Task<IReadOnlyList<PreventiveMaintenancePlan>> ListActionableAsync(DateOnly today, int maxReminderDays)
    {
        var horizon = today.AddDays(maxReminderDays);
        return await context.Set<PreventiveMaintenancePlan>()
            .Where(p => p.IsActive && p.NextDueOn <= horizon)
            .OrderBy(p => p.NextDueOn).ThenBy(p => p.Id)
            .ToListAsync();
    }
}
