using RoomTrack.Notifications.Domain.Model.Aggregates;

namespace RoomTrack.Notifications.Domain.Repositories;

public interface IPushDeviceRepository
{
    Task AddAsync(PushDevice device);
    void Remove(PushDevice device);
    Task<PushDevice?> FindByIdAsync(int id);
    Task<PushDevice?> FindByTokenHashAsync(string tokenHash);
    Task<IReadOnlyList<PushDevice>> ListByTokenHashesAsync(IReadOnlyCollection<string> tokenHashes);
    Task<IReadOnlyList<PushDevice>> ListByUsersAsync(IReadOnlyCollection<int> userIds);
}

public interface INotificationPreferencesRepository
{
    Task AddAsync(NotificationPreferences preferences);
    Task<NotificationPreferences?> FindByUserIdAsync(int userId);
    Task<IReadOnlyList<NotificationPreferences>> ListByUsersAsync(IReadOnlyCollection<int> userIds);
}
