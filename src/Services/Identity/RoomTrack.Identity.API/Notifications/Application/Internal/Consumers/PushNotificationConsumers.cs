using RoomTrack.Notifications.Domain.Model.Commands;
using RoomTrack.Notifications.Domain.Model.Queries;
using RoomTrack.Notifications.Domain.Services;
using MassTransit;
using RoomTrack.Contracts.Messaging;

namespace RoomTrack.Notifications.Application.Internal.Consumers;

/// <summary>
///     Turns a notification for some users into a delivery to their devices: only the devices of the users that did
///     not turn its kind off. Nothing is sent to users without devices.
/// </summary>
public class SendPushNotificationConsumer(
    IPushNotificationQueryService queryService,
    ILogger<SendPushNotificationConsumer> logger) : IConsumer<SendPushNotification>
{
    /// <summary>The push provider accepts up to 500 tokens per multicast; one delivery message per batch.</summary>
    public const int MaxTokensPerDelivery = 500;

    public async Task Consume(ConsumeContext<SendPushNotification> context)
    {
        var message = context.Message;
        var tokens = await queryService.Handle(new GetPushRecipientTokensQuery(message.UserIds, message.Category));
        if (tokens.Count == 0)
        {
            logger.LogDebug("Push {Category} from {Source}: no device of {Count} user(s) wants it",
                message.Category, message.SourceService, message.UserIds.Count);
            return;
        }

        foreach (var batch in tokens.Chunk(MaxTokensPerDelivery))
            await context.Publish(new DeliverPushNotification(batch, message.Category, message.Title, message.Body,
                message.Data, message.Urgent, message.SourceService));
    }
}

/// <summary>Forgets the devices whose tokens the push provider rejected for good.</summary>
public class PushDeviceTokensRejectedConsumer(IPushNotificationCommandService commandService)
    : IConsumer<PushDeviceTokensRejected>
{
    public Task Consume(ConsumeContext<PushDeviceTokensRejected> context) =>
        commandService.Handle(new ForgetRejectedPushDevicesCommand(context.Message.DeviceTokens));
}
