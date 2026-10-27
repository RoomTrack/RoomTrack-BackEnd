namespace RoomTrack.Shared.Application.OutboundServices;

/// <summary>
///     A push notification for some users (US-22).
/// </summary>
/// <param name="UserIds">Recipients (IAM user ids).</param>
/// <param name="Category">Kind of notification (<c>RoomTrack.Contracts.Messaging.PushCategories</c>); users can turn each kind off.</param>
/// <param name="Title">Title.</param>
/// <param name="Body">Text.</param>
/// <param name="Data">Data for the app, e.g. <c>{ "screen": "cleaning-task", "id": "12" }</c>.</param>
/// <param name="Urgent">Highlighted and delivered with high priority.</param>
public sealed record PushMessage(
    IReadOnlyCollection<int> UserIds,
    string Category,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data = null,
    bool Urgent = false);

/// <summary>
///     Outbound port used by the application layer of every bounded context to notify users on their devices.
/// </summary>
/// <remarks>
///     Like <see cref="IEmailSender"/>, the notification is enlisted in the current unit of work (transactional
///     outbox): it leaves only if the change it announces commits, and never makes the request wait for the provider.
/// </remarks>
public interface IPushSender
{
    /// <summary>Accepts <paramref name="message"/> for delivery once the current unit of work commits.</summary>
    Task SendAsync(PushMessage message, CancellationToken cancellationToken = default);
}
