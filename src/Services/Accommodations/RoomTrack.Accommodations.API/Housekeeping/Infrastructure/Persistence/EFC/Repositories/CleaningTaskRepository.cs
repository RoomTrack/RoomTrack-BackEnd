using RoomTrack.Housekeeping.Domain.Model.Aggregates;
using RoomTrack.Housekeeping.Domain.Model.Queries;
using RoomTrack.Housekeeping.Domain.Model.ValueObjects;
using RoomTrack.Housekeeping.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Housekeeping.Infrastructure.Persistence.EFC.Repositories;

public class CleaningTaskRepository(AppDbContext context) : ICleaningTaskRepository
{
    public async Task AddAsync(CleaningTask task) => await context.Set<CleaningTask>().AddAsync(task);

    public Task<CleaningTask?> FindByIdAsync(int id) =>
        context.Set<CleaningTask>().Include(t => t.History).FirstOrDefaultAsync(t => t.Id == id);

    public async Task<IReadOnlyList<CleaningTask>> ListAsync(GetCleaningTasksQuery query)
    {
        var tasks = context.Set<CleaningTask>().Where(t => t.HotelId == query.HotelId);
        if (query.Status is { } status) tasks = tasks.Where(t => t.Status == status);
        if (query.OnlyOpen) tasks = tasks.Where(t => t.Status == CleaningTaskStatus.Pending || t.Status == CleaningTaskStatus.InProgress);
        if (query.Priority is { } priority) tasks = tasks.Where(t => t.Priority == priority);
        if (query.RoomId is { } roomId) tasks = tasks.Where(t => t.RoomId == roomId);
        if (query.AssigneeUserId is { } assigneeUserId) tasks = tasks.Where(t => t.AssigneeUserId == assigneeUserId);

        // Priorities are stored by name: ranked explicitly so Urgent comes first, then the oldest task (US-06 list).
        return await tasks
            .OrderByDescending(t => t.Priority == CleaningTaskPriority.Urgent ? 3
                : t.Priority == CleaningTaskPriority.High ? 2
                : t.Priority == CleaningTaskPriority.Normal ? 1 : 0)
            .ThenBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .Take(query.Limit)
            .ToListAsync();
    }

    public async Task<CleaningTask?> FindOpenByRoomAsync(int roomId)
    {
        // A task added in this unit of work (not saved yet) counts too; tracked tasks are checked by their current state.
        var tracked = context.ChangeTracker.Entries<CleaningTask>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(t => t.RoomId == roomId && t.IsOpen);
        if (tracked is not null) return tracked;
        var stored = await context.Set<CleaningTask>().Include(t => t.History)
            .Where(t => t.RoomId == roomId
                        && (t.Status == CleaningTaskStatus.Pending || t.Status == CleaningTaskStatus.InProgress))
            .OrderBy(t => t.Id)
            .ToListAsync();
        return stored.FirstOrDefault(t => t.IsOpen);
    }

    public async Task<IReadOnlyDictionary<int, int>> CountOpenByAssigneeAsync(int hotelId) =>
        await context.Set<CleaningTask>()
            .Where(t => t.HotelId == hotelId && t.AssigneeUserId != null
                        && (t.Status == CleaningTaskStatus.Pending || t.Status == CleaningTaskStatus.InProgress))
            .GroupBy(t => t.AssigneeUserId!.Value)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.UserId, row => row.Count);
}

public class HotelHousekeepingSettingsRepository(AppDbContext context) : IHotelHousekeepingSettingsRepository
{
    public Task<HotelHousekeepingSettings?> FindByHotelIdAsync(int hotelId) =>
        context.Set<HotelHousekeepingSettings>().FirstOrDefaultAsync(s => s.HotelId == hotelId);

    public async Task AddAsync(HotelHousekeepingSettings settings) =>
        await context.Set<HotelHousekeepingSettings>().AddAsync(settings);
}
