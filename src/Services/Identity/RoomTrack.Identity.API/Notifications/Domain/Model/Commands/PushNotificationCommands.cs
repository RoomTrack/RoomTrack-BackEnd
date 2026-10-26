using RoomTrack.Notifications.Domain.Model.ValueObjects;

namespace RoomTrack.Notifications.Domain.Model.Commands;

/// <summary>The app of <paramref name="UserId"/> registers the token of its device to receive notifications.</summary>
public record RegisterPushDeviceCommand(int UserId, string Token, PushPlatform Platform);

/// <summary>The user stops receiving notifications on one of their devices (e.g. when signing out).</summary>
public record UnregisterPushDeviceCommand(int UserId, int DeviceId);

/// <summary>The user chooses the kinds of notification they do not want.</summary>
public record UpdateNotificationPreferencesCommand(int UserId, IReadOnlyCollection<string> DisabledCategories);

/// <summary>The push provider rejected these tokens for good: their devices are forgotten.</summary>
public record ForgetRejectedPushDevicesCommand(IReadOnlyCollection<string> Tokens);
