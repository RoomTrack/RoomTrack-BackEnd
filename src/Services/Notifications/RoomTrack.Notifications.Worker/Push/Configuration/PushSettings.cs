using Microsoft.Extensions.Options;

namespace RoomTrack.Notifications.Worker.Push.Configuration;

/// <summary>
///     Push notification delivery settings (section <c>Push</c>, env vars <c>Push__*</c>).
/// </summary>
/// <remarks>
///     <c>Push:Transport</c> is <see cref="PushTransportKind.Fcm"/> (Firebase Cloud Messaging HTTP v1, with the JSON key
///     of a service account of the Firebase project) or <see cref="PushTransportKind.Log"/> (the notification is written
///     to the log, the default while no Firebase project is configured).
/// </remarks>
public class PushSettings
{
    public const string SectionName = "Push";

    public PushTransportKind Transport { get; set; } = PushTransportKind.Log;

    public FcmSettings Fcm { get; set; } = new();

    public class FcmSettings
    {
        /// <summary>
        ///     The JSON key of a service account with the "Firebase Cloud Messaging API Admin" role (the whole file, e.g.
        ///     in <c>Push__Fcm__ServiceAccountJson</c>). Secret: never logged.
        /// </summary>
        public string? ServiceAccountJson { get; set; }

        /// <summary>The Firebase project; the <c>project_id</c> of the key when empty.</summary>
        public string? ProjectId { get; set; }

        public string BaseUrl { get; set; } = "https://fcm.googleapis.com";

        /// <summary>Timeout of one call to FCM.</summary>
        public int TimeoutSeconds { get; set; } = 10;
    }
}

public enum PushTransportKind
{
    Log,
    Fcm
}

/// <summary>Fails the start of the worker when FCM is chosen without a usable service account key.</summary>
public class PushSettingsValidator : IValidateOptions<PushSettings>
{
    public ValidateOptionsResult Validate(string? name, PushSettings settings)
    {
        if (settings.Transport != PushTransportKind.Fcm) return ValidateOptionsResult.Success;
        if (string.IsNullOrWhiteSpace(settings.Fcm.ServiceAccountJson))
            return ValidateOptionsResult.Fail("Push:Transport is Fcm: set Push__Fcm__ServiceAccountJson (the JSON key of a service account).");
        try
        {
            var key = Transport.GoogleServiceAccountKey.Parse(settings.Fcm.ServiceAccountJson);
            return string.IsNullOrWhiteSpace(settings.Fcm.ProjectId ?? key.ProjectId)
                ? ValidateOptionsResult.Fail("Push:Transport is Fcm: the key has no project_id; set Push__Fcm__ProjectId.")
                : ValidateOptionsResult.Success;
        }
        catch (Exception exception) when (exception is FormatException or System.Text.Json.JsonException or ArgumentException)
        {
            return ValidateOptionsResult.Fail($"Push__Fcm__ServiceAccountJson is not a valid service account key: {exception.Message}");
        }
    }
}
