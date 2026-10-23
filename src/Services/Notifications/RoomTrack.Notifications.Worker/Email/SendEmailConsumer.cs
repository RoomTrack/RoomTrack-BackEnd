using MassTransit;
using RoomTrack.Contracts.Messaging;
using RoomTrack.Notifications.Worker.Email.Transport;

namespace RoomTrack.Notifications.Worker.Email;

/// <summary>
///     Delivers the <see cref="SendEmail"/> messages of every service through the configured transport.
/// </summary>
/// <remarks>
///     A transient failure (timeout, connection error, 429, 5xx) is thrown, so MassTransit retries the message
///     later with an exponential delay (<c>Email:Retry</c>); after the last attempt it lands in the
///     <c>_error</c> queue, where it can be inspected and moved back from the RabbitMQ console. A permanent
///     rejection (invalid recipient, bad sender...) cannot succeed on retry: it is logged and dropped.
/// </remarks>
public class SendEmailConsumer(IEmailTransport transport, ILogger<SendEmailConsumer> logger) : IConsumer<SendEmail>
{
    public async Task Consume(ConsumeContext<SendEmail> context)
    {
        var message = context.Message;
        var recipient = EmailAddressMask.Mask(message.To);
        try
        {
            await transport.DeliverAsync(new EmailMessage(message.To, message.Subject, message.HtmlBody, message.TextBody),
                context.CancellationToken);
            logger.LogInformation("E-mail '{Subject}' from {Service} sent to {Recipient}.", message.Subject, message.SourceService, recipient);
        }
        catch (EmailDeliveryException exception) when (exception.IsPermanent)
        {
            logger.LogError("E-mail '{Subject}' from {Service} to {Recipient} was rejected permanently; giving up. {Error}",
                message.Subject, message.SourceService, recipient, exception.Message);
        }
        catch (Exception exception)
        {
            var attempt = context.GetRetryAttempt() + context.GetRedeliveryCount() + 1;
            logger.LogWarning("E-mail '{Subject}' to {Recipient} failed (attempt {Attempt}); it will be retried. {Error}",
                message.Subject, recipient, attempt, exception.Message);
            throw;
        }
    }
}
