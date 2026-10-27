using System.Security.Cryptography;
using System.Text;
using RoomTrack.Notifications.Domain.Model.Exceptions;
using RoomTrack.Notifications.Domain.Model.ValueObjects;
using RoomTrack.Shared.Domain.Model.Exceptions;

namespace RoomTrack.Notifications.Domain.Model.Aggregates;

/// <summary>
///     A device (the mobile app or a browser) where a user receives push notifications (US-22), identified by the
///     token the push provider (Firebase Cloud Messaging) gave the app.
/// </summary>
/// <remarks>
///     A token belongs to one device, so it belongs to at most one user: when someone else signs in on the same device,
///     registering the token again moves it to them. The token is looked up by its SHA-256 (<see cref="TokenHash"/>,
///     indexed): the token itself is too long for a MySQL index.
/// </remarks>
public class PushDevice
{
    public const int MaxTokenLength = 4096;

    /// <summary>EF Core constructor.</summary>
    protected PushDevice()
    {
        Token = string.Empty;
        TokenHash = string.Empty;
    }

    private PushDevice(int userId, string token, PushPlatform platform, DateTimeOffset now)
    {
        UserId = userId;
        Token = token;
        TokenHash = HashOf(token);
        Platform = platform;
        RegisteredAt = now;
        LastRegisteredAt = now;
    }

    public int Id { get; private set; }

    /// <summary>The user who receives the notifications on this device.</summary>
    public int UserId { get; private set; }

    /// <summary>The token of the push provider.</summary>
    public string Token { get; private set; }

    /// <summary>SHA-256 of <see cref="Token"/> (hex), unique.</summary>
    public string TokenHash { get; private set; }

    public PushPlatform Platform { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }

    /// <summary>Last time the app registered the token (it does on every sign-in and when the token changes).</summary>
    public DateTimeOffset LastRegisteredAt { get; private set; }

    public static PushDevice Register(int userId, string token, PushPlatform platform, DateTimeOffset now)
    {
        if (userId <= 0) throw new DomainValidationException(NotificationErrorCodes.InternalInvariant, "A device must belong to a user.");
        return new PushDevice(userId, Normalize(token), platform, now);
    }

    /// <summary>The app registered the token again, maybe for another user who signed in on the same device.</summary>
    public void RegisterAgain(int userId, PushPlatform platform, DateTimeOffset now)
    {
        UserId = userId;
        Platform = platform;
        LastRegisteredAt = now;
    }

    public static string HashOf(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(token)))).ToLowerInvariant();

    private static string Normalize(string? token)
    {
        var trimmed = token?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxTokenLength || trimmed.Any(char.IsWhiteSpace))
            throw new InvalidFieldException("token", NotificationErrorCodes.TokenInvalid,
                $"Send the device token given by the push provider (1 to {MaxTokenLength} characters, no spaces).");
        return trimmed;
    }
}
