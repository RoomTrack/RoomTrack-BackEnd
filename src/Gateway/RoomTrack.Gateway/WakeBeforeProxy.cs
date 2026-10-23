using System.Collections.Concurrent;
using Yarp.ReverseProxy.Forwarder;

namespace RoomTrack.Gateway;

/// <summary>
///     Wakes the destination before forwarding a request to it. A sleeping service on Render's free plan makes the
///     proxied request fail at once (502) instead of waiting for it to boot, and so can a request to its
///     <c>/health</c> while it boots. So when the gateway has not seen a destination answer for
///     <see cref="AwakeWindow"/>, it polls that destination's <c>/health</c> until it answers with a success status
///     or <see cref="WakeTimeout"/> passes (concurrent requests share one wake) before forwarding. Destinations that
///     answered recently without a 5xx are forwarded without any extra call.
/// </summary>
public static class WakeBeforeProxy
{
    private const string HttpClientName = "wake-before-proxy";

    /// <summary>Shorter than the ~15 minutes of inactivity after which Render puts a free service to sleep.</summary>
    private static readonly TimeSpan AwakeWindow = TimeSpan.FromMinutes(10);

    /// <summary>A free service takes about a minute to boot.</summary>
    private static readonly TimeSpan WakeTimeout = TimeSpan.FromSeconds(90);

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public static IServiceCollection AddWakeBeforeProxy(this IServiceCollection services) =>
        services.AddHttpClient(HttpClientName, client => client.Timeout = WakeTimeout).Services;

    public static IReverseProxyApplicationBuilder UseWakeBeforeProxy(this IReverseProxyApplicationBuilder proxy)
    {
        var clientFactory = proxy.ApplicationServices.GetRequiredService<IHttpClientFactory>();
        var logger = proxy.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(WakeBeforeProxy));
        var lastAwake = new ConcurrentDictionary<string, long>();
        var wakes = new ConcurrentDictionary<string, Lazy<Task>>();

        proxy.Use(async (context, next) =>
        {
            var address = context.GetReverseProxyFeature().AvailableDestinations.FirstOrDefault()?.Model.Config.Address;
            if (address is not null && !IsAwake(lastAwake, address))
            {
                var wake = wakes.GetOrAdd(address, _ => new Lazy<Task>(() =>
                    WakeAsync(clientFactory.CreateClient(HttpClientName), address, lastAwake, logger)));
                try
                {
                    await wake.Value.WaitAsync(context.RequestAborted);
                }
                finally
                {
                    wakes.TryRemove(new KeyValuePair<string, Lazy<Task>>(address, wake));
                }
            }

            await next();

            // A 5xx may be Render answering for a destination that went to sleep or is restarting.
            if (address is not null && context.Features.Get<IForwarderErrorFeature>() is null &&
                context.Response.StatusCode < StatusCodes.Status500InternalServerError)
                lastAwake[address] = Environment.TickCount64;
        });
        return proxy;
    }

    private static bool IsAwake(ConcurrentDictionary<string, long> lastAwake, string address) =>
        lastAwake.TryGetValue(address, out var last) && Environment.TickCount64 - last < AwakeWindow.TotalMilliseconds;

    private static async Task WakeAsync(HttpClient client, string address, ConcurrentDictionary<string, long> lastAwake,
        ILogger logger)
    {
        var url = address.TrimEnd('/') + "/health";
        using var timeout = new CancellationTokenSource(WakeTimeout);
        string? lastError = null;
        try
        {
            // Render answers 502 while the service boots: keep polling until the service itself answers.
            while (true)
            {
                try
                {
                    using var response = await client.GetAsync(url, timeout.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        lastAwake[address] = Environment.TickCount64;
                        return;
                    }

                    lastError = $"status {(int)response.StatusCode}";
                }
                catch (HttpRequestException exception)
                {
                    lastError = exception.Message;
                }

                await Task.Delay(RetryDelay, timeout.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Forward anyway: the request then fails as it would have without waking.
            logger.LogWarning("Wake before proxy: {Url} did not wake up in {Timeout} ({Error})", url, WakeTimeout,
                lastError ?? "timeout");
        }
    }
}
