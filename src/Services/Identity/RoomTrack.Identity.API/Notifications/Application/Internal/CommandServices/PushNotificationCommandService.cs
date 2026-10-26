using RoomTrack.Notifications.Domain.Model.Aggregates;
using RoomTrack.Notifications.Domain.Model.Commands;
using RoomTrack.Notifications.Domain.Repositories;
using RoomTrack.Notifications.Domain.Services;
using RoomTrack.Shared.Domain.Repositories;

namespace RoomTrack.Notifications.Application.Internal.CommandServices;

/// <summary>Devices and notification preferences of the users (US-22).</summary>
public class PushNotificationCommandService(
    IPushDeviceRepository deviceRepository,
    INotificationPreferencesRepository preferencesRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IPushNotificationCommandService
{
    public async Task<PushDevice> Handle(RegisterPushDeviceCommand command)
    {
        var now = timeProvider.GetUtcNow();
        // Registering is idempotent: the app does it on every sign-in. A token already known moves to this user.
        var device = await deviceRepository.FindByTokenHashAsync(PushDevice.HashOf(command.Token));
        if (device is null)
        {
            device = PushDevice.Register(command.UserId, command.Token, command.Platform, now);
            await deviceRepository.AddAsync(device);
        }
        else
        {
            device.RegisterAgain(command.UserId, command.Platform, now);
        }

        await unitOfWork.CompleteAsync();
        return device;
    }

    public async Task<bool> Handle(UnregisterPushDeviceCommand command)
    {
        var device = await deviceRepository.FindByIdAsync(command.DeviceId);
        if (device is null || device.UserId != command.UserId) return false;

        deviceRepository.Remove(device);
        await unitOfWork.CompleteAsync();
        return true;
    }

    public async Task<NotificationPreferences> Handle(UpdateNotificationPreferencesCommand command)
    {
        var preferences = await preferencesRepository.FindByUserIdAsync(command.UserId);
        if (preferences is null)
        {
            preferences = new NotificationPreferences(command.UserId);
            await preferencesRepository.AddAsync(preferences);
        }

        preferences.Disable(command.DisabledCategories, timeProvider.GetUtcNow());
        await unitOfWork.CompleteAsync();
        return preferences;
    }

    public async Task<int> Handle(ForgetRejectedPushDevicesCommand command)
    {
        var hashes = command.Tokens.Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(PushDevice.HashOf).Distinct().ToList();
        if (hashes.Count == 0) return 0;

        var devices = await deviceRepository.ListByTokenHashesAsync(hashes);
        foreach (var device in devices) deviceRepository.Remove(device);
        await unitOfWork.CompleteAsync();
        return devices.Count;
    }
}
