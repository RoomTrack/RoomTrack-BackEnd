namespace RoomTrack.Notifications.Domain.Model.Queries;

/// <summary>The devices of a user.</summary>
public record GetPushDevicesOfUserQuery(int UserId);

/// <summary>The notification preferences of a user (all kinds on when they never chose).</summary>
public record GetNotificationPreferencesQuery(int UserId);

/// <summary>The device tokens of the users that did not turn <paramref name="Category"/> off.</summary>
public record GetPushRecipientTokensQuery(IReadOnlyCollection<int> UserIds, string Category);
