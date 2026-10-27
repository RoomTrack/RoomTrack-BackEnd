using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RoomTrack.Notifications.Worker.Push.Transport;

/// <summary>
///     Firebase Cloud Messaging HTTP v1 (<c>POST /v1/projects/{project}/messages:send</c>), one request per device
///     token, authenticated with an access token of the service account (<see cref="GoogleAccessTokenProvider"/>).
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>404 <c>UNREGISTERED</c> and 400 <c>INVALID_ARGUMENT</c> mean the token is no longer valid
///         (uninstalled app, expired or malformed token): it is reported as rejected so its device is forgotten.</item>
///         <item>429, 5xx, timeouts and connection errors are transient: the token is reported as failed and
///         retried later.</item>
///         <item>401/403 (wrong key or role) fail every token: they are thrown so the message is retried and the
///         error is visible in the logs.</item>
///     </list>
///     Urgent notifications go with high priority (Android) and priority 10 (APNs); the others with normal priority.
/// </remarks>
public class FcmPushTransport(
    HttpClient httpClient,
    GoogleAccessTokenProvider accessTokens,
    string projectId,
    ILogger<FcmPushTransport> logger) : IPushTransport
{
    public const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    public async Task<PushDeliveryResult> DeliverAsync(PushDelivery delivery, CancellationToken cancellationToken)
    {
        var accessToken = await accessTokens.GetAccessTokenAsync(cancellationToken);
        List<string> delivered = [], rejected = [], failed = [];

        foreach (var token in delivery.DeviceTokens)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"v1/projects/{Uri.EscapeDataString(projectId)}/messages:send")
            {
                Content = JsonContent.Create(new { message = Message(token, delivery) })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                              && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("FCM did not answer: {Error}", exception.Message);
                failed.Add(token);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    delivered.Add(token);
                    continue;
                }

                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new HttpRequestException($"FCM refused the credentials ({(int)response.StatusCode}): {error}", null, response.StatusCode);
                if (IsInvalidToken(response.StatusCode, error))
                    rejected.Add(token);
                else
                {
                    logger.LogWarning("FCM answered {Status} for a device: {Error}", (int)response.StatusCode, error);
                    failed.Add(token);
                }
            }
        }

        return new PushDeliveryResult(delivered, rejected, failed);
    }

    private static object Message(string token, PushDelivery delivery)
    {
        var data = new Dictionary<string, string>(delivery.Data ?? new Dictionary<string, string>())
        {
            ["category"] = delivery.Category,
            ["urgent"] = delivery.Urgent ? "true" : "false"
        };
        return new
        {
            token,
            notification = new { title = delivery.Title, body = delivery.Body },
            data,
            android = new { priority = delivery.Urgent ? "HIGH" : "NORMAL" },
            apns = new { headers = new Dictionary<string, string> { ["apns-priority"] = delivery.Urgent ? "10" : "5" } }
        };
    }

    /// <summary>The token itself is the problem (FCM v1 error codes).</summary>
    private static bool IsInvalidToken(HttpStatusCode status, string error)
    {
        if (status == HttpStatusCode.NotFound) return true; // UNREGISTERED
        if (status != HttpStatusCode.BadRequest) return false;
        try
        {
            using var document = JsonDocument.Parse(error);
            return document.RootElement.TryGetProperty("error", out var body)
                   && body.TryGetProperty("status", out var code) && code.GetString() == "INVALID_ARGUMENT";
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
