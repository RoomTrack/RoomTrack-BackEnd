using RoomTrack.Notifications.Domain.Model.Aggregates;
using RoomTrack.Notifications.Domain.Model.Queries;
using RoomTrack.Notifications.Domain.Repositories;
using RoomTrack.Notifications.Domain.Services;

namespace RoomTrack.Notifications.Application.Internal.QueryServices;

public class PushNotificationQueryService(
    IPushDeviceRepository deviceRepository,
    INotificationPreferencesRepository preferencesRepository) : IPushNotificationQueryService
{
    public Task<IReadOnlyList<PushDevice>> Handle(GetPushDevicesOfUserQuery query) =>
        deviceRepository.ListByUsersAsync([query.UserId]);

    public async Task<NotificationPreferences> Handle(GetNotificationPreferencesQuery query) =>
        await preferencesRepository.FindByUserIdAsync(query.UserId) ?? new NotificationPreferences(query.UserId);

    public async Task<IReadOnlyList<string>> Handle(GetPushRecipientTokensQuery query)
    {
        var userIds = query.UserIds.Distinct().ToList();
        if (userIds.Count == 0) return [];

        var optedOut = (await preferencesRepository.ListByUsersAsync(userIds))
            .Where(preferences => !preferences.IsEnabled(query.Category))
            .Select(preferences => preferences.UserId)
            .ToHashSet();
        return (await deviceRepository.ListByUsersAsync(userIds))
            .Where(device => !optedOut.Contains(device.UserId))
            .Select(device => device.Token)
            .Distinct()
            .ToList();
    }
}
