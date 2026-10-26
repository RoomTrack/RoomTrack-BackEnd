namespace RoomTrack.Notifications.Worker.Push.Transport;

/// <summary>A push notification for some device tokens.</summary>
public sealed record PushDelivery(
    IReadOnlyList<string> DeviceTokens,
    string Category,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data,
    bool Urgent);

/// <summary>What the provider did with each token.</summary>
/// <param name="Delivered">Accepted by the provider.</param>
/// <param name="Rejected">Refused for good (app uninstalled, expired or invalid token): forget them.</param>
/// <param name="Failed">Failed for now (provider unavailable, rate limit): try them again later.</param>
public sealed record PushDeliveryResult(
    IReadOnlyList<string> Delivered,
    IReadOnlyList<string> Rejected,
    IReadOnlyList<string> Failed);

/// <summary>Hands push notifications to a provider (Firebase Cloud Messaging, or the log in development).</summary>
public interface IPushTransport
{
    Task<PushDeliveryResult> DeliverAsync(PushDelivery delivery, CancellationToken cancellationToken);
}

/// <summary>
///     Development transport (<c>Push:Transport=Log</c>): writes the notification to the log instead of sending it.
/// </summary>
public class LoggingPushTransport(ILogger<LoggingPushTransport> logger) : IPushTransport
{
    public Task<PushDeliveryResult> DeliverAsync(PushDelivery delivery, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Push:Transport is Log: push notification written to the log instead of being sent.\n" +
            "Devices: {Count}\nCategory: {Category}{Urgent}\nTitle: {Title}\n{Body}\nData: {Data}",
            delivery.DeviceTokens.Count, delivery.Category, delivery.Urgent ? " (urgent)" : string.Empty, delivery.Title,
            delivery.Body, delivery.Data is null ? "-" : string.Join(", ", delivery.Data.Select(pair => $"{pair.Key}={pair.Value}")));
        return Task.FromResult(new PushDeliveryResult(delivery.DeviceTokens, [], []));
    }
}
