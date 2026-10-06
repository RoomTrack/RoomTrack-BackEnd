using System.Collections.Concurrent;
using Yarp.ReverseProxy.Forwarder;

namespace RoomTrack.Gateway;

/// <summary>
///     Wakes the destination before forwarding a request to it. A sleeping service on Render's free plan makes the
///     proxied request fail at once (502) instead of waiting for it to boot, while a plain request to its
///     <c>/health</c> waits until it is up. So when the gateway has not seen a destination answer for
///     <see cref="AwakeWindow"/>, it awaits that destination's <c>/health</c> (concurrent requests share one call)
///     before forwarding. Destinations that answered recently are forwarded without any extra call.
/// </summary>
public static class WakeBeforeProxy
{
    private const string HttpClientName = "wake-before-proxy";

    /// <summary>Shorter than the ~15 minutes of inactivity after which Render puts a free service to sleep.</summary>
    private static readonly TimeSpan AwakeWindow = TimeSpan.FromMinutes(10);

    public static IServiceCollection AddWakeBeforeProxy(this IServiceCollection services) =>
        services.AddHttpClient(HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(90)).Services;

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

            if (address is not null && context.Features.Get<IForwarderErrorFeature>() is null)
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
        try
        {
            // Any answer means the service is up; an unhealthy one still gets the request, as before.
            using var response = await client.GetAsync(url);
            lastAwake[address] = Environment.TickCount64;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Wake before proxy: {Url} did not answer ({Error})", url, exception.Message);
        }
    }
}
