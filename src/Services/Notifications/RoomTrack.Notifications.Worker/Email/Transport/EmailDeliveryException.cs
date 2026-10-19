namespace RoomTrack.Notifications.Worker.Email.Transport;

/// <summary>
///     The mail system refused a message. <see cref="IsPermanent"/> tells the worker whether a retry can succeed.
///     The message is a short, secret-free description (it is written to the log).
/// </summary>
public sealed class EmailDeliveryException(string message, bool isPermanent, Exception? innerException = null)
    : Exception(message, innerException)
{
    public bool IsPermanent { get; } = isPermanent;
}
