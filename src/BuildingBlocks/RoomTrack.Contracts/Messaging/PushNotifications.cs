namespace RoomTrack.Contracts.Messaging;

/// <summary>
///     Command message: notify these users on their devices (US-22). Sent by any service through its transactional
///     outbox and consumed by the Identity service, which knows the devices each user registered and the
///     notifications each one turned off; it then asks the Notifications worker to deliver it
///     (<see cref="DeliverPushNotification"/>).
/// </summary>
/// <param name="UserIds">The recipients (IAM user ids).</param>
/// <param name="Category">What it is about (<see cref="PushCategories"/>): the users choose which ones they get.</param>
/// <param name="Title">Title of the notification.</param>
/// <param name="Body">Text of the notification.</param>
/// <param name="Data">Data for the app (e.g. the screen to open), all string values.</param>
/// <param name="Urgent">Highlighted and delivered with high priority (e.g. a task that became urgent).</param>
/// <param name="SourceService">Service that asked for it (for the logs).</param>
public sealed record SendPushNotification(
    IReadOnlyList<int> UserIds,
    string Category,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data,
    bool Urgent,
    string SourceService);

/// <summary>
///     Command message: deliver a notification to these device tokens. Sent by the Identity service and consumed by
///     the Notifications worker, which owns the connection to the push provider (Firebase Cloud Messaging or the log).
/// </summary>
public sealed record DeliverPushNotification(
    IReadOnlyList<string> DeviceTokens,
    string Category,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data,
    bool Urgent,
    string SourceService);

/// <summary>
///     Event: the push provider rejected these device tokens for good (the app was uninstalled or the token expired).
///     Published by the Notifications worker; the Identity service forgets the devices.
/// </summary>
public sealed record PushDeviceTokensRejected(IReadOnlyList<string> DeviceTokens, DateTimeOffset OccurredOn);

/// <summary>Kinds of push notifications. Each user can turn any of them off.</summary>
public static class PushCategories
{
    /// <summary>A cleaning task was assigned to (or taken from) the user.</summary>
    public const string CleaningTaskAssigned = "cleaning_task_assigned";

    /// <summary>The priority of the user's task or incident changed (urgent ones are highlighted).</summary>
    public const string PriorityChanged = "priority_changed";

    /// <summary>A new incident for maintenance, or one assigned to the user.</summary>
    public const string IncidentAssigned = "incident_assigned";

    /// <summary>A critical incident was not attended in time (administrators).</summary>
    public const string IncidentEscalated = "incident_escalated";

    /// <summary>A guest request for the user's area, or news of the request the guest made.</summary>
    public const string GuestRequest = "guest_request";

    /// <summary>A new message in a conversation of the user.</summary>
    public const string Message = "message";

    public static readonly IReadOnlyList<string> All =
        [CleaningTaskAssigned, PriorityChanged, IncidentAssigned, IncidentEscalated, GuestRequest, Message];
}
