using MassTransit;
using RoomTrack.Contracts.Messaging;
using RoomTrack.Notifications.Worker.Push.Transport;

namespace RoomTrack.Notifications.Worker.Push;

/// <summary>
///     Delivers the <see cref="DeliverPushNotification"/> messages of the Identity service through the configured
///     transport.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>Tokens the provider rejected for good are published as <see cref="PushDeviceTokensRejected"/> so
///         Identity forgets their devices.</item>
///         <item>When no device could be reached for now, the message is thrown back and retried with backoff
///         (nobody got it yet, so retrying sends no duplicates). When only some failed, a new message is sent for
///         those, so the devices that already got it are not notified twice.</item>
///     </list>
/// </remarks>
public class DeliverPushNotificationConsumer(IPushTransport transport, TimeProvider timeProvider,
    ILogger<DeliverPushNotificationConsumer> logger) : IConsumer<DeliverPushNotification>
{
    public async Task Consume(ConsumeContext<DeliverPushNotification> context)
    {
        var message = context.Message;
        var result = await transport.DeliverAsync(new PushDelivery(message.DeviceTokens, message.Category, message.Title,
            message.Body, message.Data, message.Urgent), context.CancellationToken);

        if (result.Rejected.Count > 0)
            await context.Publish(new PushDeviceTokensRejected(result.Rejected, timeProvider.GetUtcNow()));

        if (result.Failed.Count > 0 && result.Delivered.Count == 0 && result.Rejected.Count == 0)
            throw new HttpRequestException(
                $"Push '{message.Category}' from {message.SourceService}: no device of {result.Failed.Count} could be reached; it will be retried.");
        if (result.Failed.Count > 0)
            await context.Publish(message with { DeviceTokens = result.Failed });

        logger.LogInformation("Push '{Category}' from {Service}: {Delivered} delivered, {Rejected} rejected, {Failed} to retry.",
            message.Category, message.SourceService, result.Delivered.Count, result.Rejected.Count, result.Failed.Count);
    }
}
