namespace RoomTrack.Notifications.Domain.Model.Exceptions;

/// <summary>Stable error codes of the push notifications context (devices and preferences).</summary>
public static class NotificationErrorCodes
{
    public const string TokenInvalid = "push.token_invalid";
    public const string CategoryUnknown = "push.category_unknown";

    /// <summary>An invariant that only a programming error can break.</summary>
    public const string InternalInvariant = "push.internal_invariant";
}
