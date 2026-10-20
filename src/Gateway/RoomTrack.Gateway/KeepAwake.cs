namespace RoomTrack.Gateway;

/// <summary>
///     Keeps the services awake while the app is in use. On Render's free plan every service sleeps after ~15 minutes
///     without inbound traffic and the first request then waits for it to boot; the services also call each other
///     (and the notifications worker only consumes RabbitMQ), so a sleeping one can fail a request that reached an
///     awake one. Whenever the gateway receives a request, and at most once per <c>KeepAwake:IntervalMinutes</c>, it
///     pings the <c>/health</c> of every URL in <c>KeepAwake:Urls</c> in the background. Without traffic everything
///     goes to sleep, which saves free instance hours. Disabled when no URL is configured.
/// </summary>
public static class KeepAwake
{
    private const string SectionName = "KeepAwake";
    private const string HttpClientName = "keep-awake";

    public static IServiceCollection AddKeepAwake(this IServiceCollection services) =>
        services.AddHttpClient(HttpClientName, client => client.Timeout = TimeSpan.FromMinutes(2)).Services;

    public static WebApplication UseKeepAwake(this WebApplication app)
    {
        var urls = (app.Configuration[$"{SectionName}:Urls"] ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(url => url.TrimEnd('/') + "/health")
            .ToArray();
        if (urls.Length == 0) return app;

        var interval = TimeSpan.FromMinutes(app.Configuration.GetValue($"{SectionName}:IntervalMinutes", 5));
        var clientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
        var logger = app.Logger;
        long lastPingTicks = 0;

        logger.LogInformation("Keep-awake: pinging {Count} services every {Interval} while there is traffic", urls.Length, interval);

        app.Use((context, next) =>
        {
            var now = DateTime.UtcNow.Ticks;
            var last = Interlocked.Read(ref lastPingTicks);
            // Only the request that wins the exchange pings, so concurrent requests do not ping twice.
            if (now - last >= interval.Ticks && Interlocked.CompareExchange(ref lastPingTicks, now, last) == last)
                _ = Task.Run(() => PingAllAsync(clientFactory.CreateClient(HttpClientName), urls, logger));
            return next(context);
        });
        return app;
    }

    private static Task PingAllAsync(HttpClient client, string[] urls, ILogger logger) =>
        Task.WhenAll(urls.Select(async url =>
        {
            try
            {
                using var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    logger.LogWarning("Keep-awake: {Url} answered {StatusCode}", url, (int)response.StatusCode);
            }
            catch (Exception exception)
            {
                logger.LogWarning("Keep-awake: {Url} did not answer ({Error})", url, exception.Message);
            }
        }));
}
