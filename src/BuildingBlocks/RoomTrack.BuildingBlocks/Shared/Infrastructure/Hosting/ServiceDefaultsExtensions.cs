using BackendAwRoomTrack.API.IAM.Application.OutboundServices;
using BackendAwRoomTrack.API.IAM.Infrastructure.Authentication;
using BackendAwRoomTrack.API.IAM.Infrastructure.Sessions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Authentication.ScheduledJobs;
using BackendAwRoomTrack.API.Shared.Infrastructure.Documentation.OpenApi.Configuration.Extensions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Interfaces.ASP.Configuration;
using BackendAwRoomTrack.API.Shared.Infrastructure.Interfaces.ASP.Configuration.Extensions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Interfaces.ASP.RateLimiting;
using BackendAwRoomTrack.API.Shared.Infrastructure.Interfaces.ASP.ReverseProxy;
using BackendAwRoomTrack.API.Shared.Infrastructure.Internal;
using BackendAwRoomTrack.API.Shared.Infrastructure.Mediator.Cortex.Configuration.Extensions;
using BackendAwRoomTrack.API.Shared.Infrastructure.Persistence.EFC.Configuration;
using BackendAwRoomTrack.API.shared.Infrastructure.Persistence.EFC.Configuration.Extensions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace BackendAwRoomTrack.API.Shared.Infrastructure.Hosting;

/// <summary>
///     What every RoomTrack service has in common, so each <c>Program.cs</c> only adds its own bounded contexts:
///     controllers and their conventions, its database, OpenAPI, errors as ProblemDetails, JWT authentication and the
///     capability policies, the internal API between services, the scheduler key, forwarded headers, rate limiting,
///     the mediator, e-mails and health checks.
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <param name="builder">The web application builder.</param>
    /// <param name="serviceTitle">Name of the service in its OpenAPI document (e.g. "Bookings").</param>
    /// <param name="validateSessionsLocally">
    ///     True only in the Identity service, which registers its own <see cref="IUserSessionValidator"/>; the other
    ///     services ask Identity over HTTP.
    /// </param>
    public static WebApplicationBuilder AddRoomTrackServiceDefaults<TContext, TMarker>(
        this WebApplicationBuilder builder, string serviceTitle, bool validateSessionsLocally = false)
        where TContext : AppDbContext
    {
        builder.Services.AddControllers(options =>
        {
            options.Conventions.Add(new KebabCaseRouteNamingConvention());
            // Validation errors are keyed by the JSON (camelCase) property names the clients send.
            options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        });

        builder.AddDatabaseConfigurationServices<TContext>();
        builder.AddOpenApiConfigurationServices(serviceTitle);
        builder.AddSharedContextServices();

        // Native ASP.NET Core authentication (JWT issued by Identity) and the capability policies.
        builder.Services.AddIamAuthentication(builder.Configuration);
        builder.Services.AddIamAuthorization();
        builder.Services.AddMemoryCache();
        if (!validateSessionsLocally)
            builder.Services.AddInternalServiceClient<IUserSessionValidator, HttpUserSessionValidator>(builder.Configuration, "Identity");

        // Calls from the other services (/internal/v1, X-Internal-Key)
        builder.Services.AddInternalApi(builder.Configuration);

        builder.AddCortexMediatorServices<TMarker>();

        // X-Cron-Key authentication of the external scheduler (scheduled jobs)
        builder.Services.AddScheduledJobsAuthentication(builder.Configuration);

        // Real client IP behind the API gateway (X-Forwarded-For from trusted proxies only)
        builder.Services.AddRoomTrackForwardedHeaders(builder.Configuration);

        // Rate limiting of the anonymous endpoints, per client IP
        builder.Services.AddRoomTrackRateLimiting(builder.Configuration);

        return builder;
    }

    /// <summary>The HTTP pipeline shared by every service.</summary>
    public static WebApplication UseRoomTrackServiceDefaults(this WebApplication app)
    {
        // Forwarded headers first: the client IP and scheme must be resolved before anything reads them
        // (rate limiter partitions, audit log, error responses).
        app.UseRoomTrackForwardedHeaders();
        // Global exception handler first, so errors from every later middleware become ProblemDetails
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseOpenApiConfiguration();

        // The rate limiter runs after authentication so per-user policies see the signed-in user.
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        app.MapControllers();
        app.MapApiDocumentation();
        app.MapHealthChecks("/health").AllowAnonymous();
        return app;
    }
}
