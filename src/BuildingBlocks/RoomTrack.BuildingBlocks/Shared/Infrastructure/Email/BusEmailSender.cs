using BackendAwRoomTrack.API.Shared.Application.OutboundServices;
using MassTransit;
using RoomTrack.Contracts.Messaging;

namespace BackendAwRoomTrack.API.Shared.Infrastructure.Email;

/// <summary>
///     <see cref="IEmailSender"/> adapter of the services: the message is published as a <see cref="SendEmail"/>
///     message through the transactional outbox of the service (MassTransit bus outbox), i.e. enlisted in the current
///     unit of work. It reaches RabbitMQ only if the business change commits, and the Notifications worker delivers
///     it. Nothing is sent here: the request never waits for, nor reveals, the mail system.
/// </summary>
public class BusEmailSender(IPublishEndpoint publishEndpoint, IHostEnvironment environment) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message.To);
        return publishEndpoint.Publish(
            new SendEmail(message.To.Trim(), message.Subject, message.HtmlBody, message.TextBody, environment.ApplicationName),
            cancellationToken);
    }
}
