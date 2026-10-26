using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RoomTrack.Notifications.Worker.Push.Transport;

namespace RoomTrack.Notifications.Worker.Push.Configuration;

public static class PushServiceCollectionExtensions
{
    /// <summary>Registers the transport chosen by <c>Push:Transport</c> (<see cref="PushSettings"/>, validated at startup).</summary>
    public static IServiceCollection AddPushTransports(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PushSettings>()
            .Bind(configuration.GetSection(PushSettings.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PushSettings>, PushSettingsValidator>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient(nameof(GoogleAccessTokenProvider), client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddHttpClient(nameof(FcmPushTransport), (provider, client) =>
        {
            var fcm = provider.GetRequiredService<IOptions<PushSettings>>().Value.Fcm;
            client.BaseAddress = new Uri(fcm.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(fcm.TimeoutSeconds);
        });

        // One access token cache for the whole worker.
        services.AddSingleton(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<PushSettings>>().Value;
            return new GoogleAccessTokenProvider(
                provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GoogleAccessTokenProvider)),
                GoogleServiceAccountKey.Parse(settings.Fcm.ServiceAccountJson!), FcmPushTransport.Scope,
                provider.GetRequiredService<TimeProvider>());
        });
        services.AddSingleton<LoggingPushTransport>();
        services.AddScoped<IPushTransport>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<PushSettings>>().Value;
            if (settings.Transport != PushTransportKind.Fcm) return provider.GetRequiredService<LoggingPushTransport>();

            var key = GoogleServiceAccountKey.Parse(settings.Fcm.ServiceAccountJson!);
            return new FcmPushTransport(
                provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(FcmPushTransport)),
                provider.GetRequiredService<GoogleAccessTokenProvider>(),
                settings.Fcm.ProjectId ?? key.ProjectId!,
                provider.GetRequiredService<ILogger<FcmPushTransport>>());
        });

        return services;
    }
}
