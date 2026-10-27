using RoomTrack.Shared.Application.OutboundServices;
using MassTransit;
using RoomTrack.Contracts.Messaging;

namespace RoomTrack.Shared.Infrastructure.Push;

/// <summary>
///     <see cref="IPushSender"/> adapter of the services: published as a <see cref="SendPushNotification"/> message
///     through the transactional outbox of the service. The Identity service resolves the devices of the users and the
///     Notifications worker delivers it.
/// </summary>
public class BusPushSender(IPublishEndpoint publishEndpoint, IHostEnvironment environment) : IPushSender
{
    public Task SendAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Category);
        var userIds = message.UserIds.Distinct().ToList();
        if (userIds.Count == 0) return Task.CompletedTask;

        return publishEndpoint.Publish(
            new SendPushNotification(userIds, message.Category, message.Title, message.Body, message.Data, message.Urgent,
                environment.ApplicationName),
            cancellationToken);
    }
}
