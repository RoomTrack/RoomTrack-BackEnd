using RoomTrack.Notifications.Domain.Model.Aggregates;
using RoomTrack.Notifications.Domain.Repositories;
using RoomTrack.Shared.Infrastructure.Persistence.EFC.Configuration;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Notifications.Infrastructure.Persistence.EFC.Repositories;

public class PushDeviceRepository(AppDbContext context) : IPushDeviceRepository
{
    public async Task AddAsync(PushDevice device) => await context.Set<PushDevice>().AddAsync(device);

    public void Remove(PushDevice device) => context.Set<PushDevice>().Remove(device);

    public Task<PushDevice?> FindByIdAsync(int id) => context.Set<PushDevice>().FirstOrDefaultAsync(d => d.Id == id);

    public Task<PushDevice?> FindByTokenHashAsync(string tokenHash) =>
        context.Set<PushDevice>().FirstOrDefaultAsync(d => d.TokenHash == tokenHash);

    public async Task<IReadOnlyList<PushDevice>> ListByTokenHashesAsync(IReadOnlyCollection<string> tokenHashes) =>
        await context.Set<PushDevice>().Where(d => tokenHashes.Contains(d.TokenHash)).ToListAsync();

    public async Task<IReadOnlyList<PushDevice>> ListByUsersAsync(IReadOnlyCollection<int> userIds) =>
        await context.Set<PushDevice>().Where(d => userIds.Contains(d.UserId)).OrderBy(d => d.Id).ToListAsync();
}

public class NotificationPreferencesRepository(AppDbContext context) : INotificationPreferencesRepository
{
    public async Task AddAsync(NotificationPreferences preferences) =>
        await context.Set<NotificationPreferences>().AddAsync(preferences);

    public Task<NotificationPreferences?> FindByUserIdAsync(int userId) =>
        context.Set<NotificationPreferences>().FirstOrDefaultAsync(p => p.UserId == userId);

    public async Task<IReadOnlyList<NotificationPreferences>> ListByUsersAsync(IReadOnlyCollection<int> userIds) =>
        await context.Set<NotificationPreferences>().Where(p => userIds.Contains(p.UserId)).ToListAsync();
}
