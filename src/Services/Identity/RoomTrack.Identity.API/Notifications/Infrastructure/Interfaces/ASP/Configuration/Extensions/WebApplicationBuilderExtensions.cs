using RoomTrack.Notifications.Application.Internal.CommandServices;
using RoomTrack.Notifications.Application.Internal.Consumers;
using RoomTrack.Notifications.Application.Internal.QueryServices;
using RoomTrack.Notifications.Domain.Repositories;
using RoomTrack.Notifications.Domain.Services;
using RoomTrack.Notifications.Infrastructure.Persistence.EFC.Repositories;
using MassTransit;

namespace RoomTrack.Notifications.Infrastructure.Interfaces.ASP.Configuration.Extensions;

public static class WebApplicationBuilderExtensions
{
    /// <summary>Push notifications context: the devices and preferences of the users (US-22).</summary>
    public static void AddNotificationsContextServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IPushDeviceRepository, PushDeviceRepository>();
        builder.Services.AddScoped<INotificationPreferencesRepository, NotificationPreferencesRepository>();
        builder.Services.AddScoped<IPushNotificationCommandService, PushNotificationCommandService>();
        builder.Services.AddScoped<IPushNotificationQueryService, PushNotificationQueryService>();
    }

    /// <summary>
    ///     Consumers of the notifications every service asks for and of the tokens the push provider rejected.
    /// </summary>
    public static void AddNotificationsConsumers(this IBusRegistrationConfigurator bus)
    {
        bus.AddConsumer<SendPushNotificationConsumer>();
        bus.AddConsumer<PushDeviceTokensRejectedConsumer>();
    }
}
