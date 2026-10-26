using RoomTrack.Notifications.Domain.Model.Aggregates;
using RoomTrack.Notifications.Domain.Model.Commands;
using RoomTrack.Notifications.Domain.Model.Queries;

namespace RoomTrack.Notifications.Domain.Services;

public interface IPushNotificationCommandService
{
    Task<PushDevice> Handle(RegisterPushDeviceCommand command);

    /// <summary>False when the device does not exist or belongs to someone else.</summary>
    Task<bool> Handle(UnregisterPushDeviceCommand command);

    Task<NotificationPreferences> Handle(UpdateNotificationPreferencesCommand command);

    /// <summary>How many devices were forgotten.</summary>
    Task<int> Handle(ForgetRejectedPushDevicesCommand command);
}

public interface IPushNotificationQueryService
{
    Task<IReadOnlyList<PushDevice>> Handle(GetPushDevicesOfUserQuery query);
    Task<NotificationPreferences> Handle(GetNotificationPreferencesQuery query);
    Task<IReadOnlyList<string>> Handle(GetPushRecipientTokensQuery query);
}
