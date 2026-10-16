using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace BackendAwRoomTrack.API.Shared.Infrastructure.Internal;

public static class InternalApiServiceCollectionExtensions
{
    /// <summary>Policy of the internal endpoints: only other services, authenticated by the internal key.</summary>
    public const string InternalServicePolicy = "InternalService";

    /// <summary>
    ///     Registers the internal API key settings, its authentication scheme and the
    ///     <see cref="InternalServicePolicy"/> policy (for the service's own <c>/internal/v1</c> endpoints).
    /// </summary>
    public static IServiceCollection AddInternalApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<InternalApiSettings>()
            .Bind(configuration.GetSection(InternalApiSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, InternalApiAuthenticationHandler>(InternalApiAuthenticationHandler.SchemeName, null);

        services.AddAuthorizationBuilder()
            .AddPolicy(InternalServicePolicy, policy => policy
                .AddAuthenticationSchemes(InternalApiAuthenticationHandler.SchemeName)
                .RequireClaim(InternalApiAuthenticationHandler.ScopeClaim, InternalApiAuthenticationHandler.InternalScope));

        return services;
    }

    /// <summary>
    ///     Registers <typeparamref name="TImplementation"/> as the HTTP client of another service: base address from
    ///     <c>Services:{serviceName}:BaseUrl</c>, the internal key header, and the standard resilience pipeline
    ///     (timeouts, circuit breaker, and retries of the safe methods only: a POST is never repeated).
    /// </summary>
    public static IHttpClientBuilder AddInternalServiceClient<TClient, TImplementation>(
        this IServiceCollection services, IConfiguration configuration, string serviceName)
        where TClient : class
        where TImplementation : class, TClient
    {
        var baseUrl = configuration[$"Services:{serviceName}:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException(
                $"Services:{serviceName}:BaseUrl is not configured. Set 'Services__{serviceName}__BaseUrl' " +
                $"(e.g. http://{serviceName.ToLowerInvariant()}-service:8080).");

        var builder = services.AddHttpClient<TClient, TImplementation>((provider, client) =>
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add(InternalApiAuthenticationHandler.HeaderName,
                provider.GetRequiredService<IOptions<InternalApiSettings>>().Value.Key);
        });
        builder.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());
        return builder;
    }
}
