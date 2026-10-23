using Microsoft.OpenApi.Models;
using BackendAwRoomTrack.API.Shared.Infrastructure.Documentation.OpenApi.Configuration;

namespace BackendAwRoomTrack.API.Shared.Infrastructure.Documentation.OpenApi.Configuration.Extensions;

/// <summary>
/// Extension methods for configuring the OpenAPI documentation of a service (CORS is handled by the API gateway).
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Configures OpenAPI/Swagger documentation services with JWT authentication support.
    /// </summary>
    /// <param name="builder">The web application builder instance.</param>
    /// <param name="serviceTitle">Name of the service in the document title (e.g. "Bookings").</param>
    public static void AddOpenApiConfigurationServices(this WebApplicationBuilder builder, string serviceTitle)
    {
        builder.Services.AddOpenApi();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            // Configure API documentation metadata
            options.SwaggerDoc("v1",
                new OpenApiInfo
                {
                    Title = $"RoomTrack {serviceTitle} API",
                    Version = "v1",
                    Description = """
                        REST API of RoomTrack, the hotel management platform: accounts and access (authentication,
                        users, audit log), hotels and rooms, bookings and payments, guest and staff profiles, analytics
                        and demo requests from the landing.

                        **Authentication.** Sign in with `POST /api/v1/authentication/sign-in` and send the returned
                        `token` as `Authorization: Bearer <token>`. Access tokens last 30 minutes; with `rememberMe`
                        the response also carries a refresh token for `POST /api/v1/authentication/refresh`.

                        **Errors.** Every error is an RFC 7807 ProblemDetails (`application/problem+json`): read
                        `detail`; validation errors list each invalid field in `errors`.
                        """,
                    Contact = new OpenApiContact
                    {
                        Name = "RoomTrack",
                        Email = "contact@roomtrack.com"
                    },
                    License = new OpenApiLicense
                    {
                        Name = "Apache 2.0",
                        Url = new Uri("https://www.apache.org/licenses/LICENSE-2.0.html")
                    }
                });
            
            // JWT bearer scheme: the "Authorize" button of Swagger UI sends "Authorization: Bearer <token>".
            options.AddSecurityDefinition(BearerSecurityRequirementOperationFilter.SchemeId, new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Paste the token returned by POST /api/v1/authentication/sign-in (without the 'Bearer ' prefix).",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "bearer"
            });

            // Shared secret of the external scheduler (only for the scheduled job endpoints).
            options.AddSecurityDefinition(BearerSecurityRequirementOperationFilter.CronKeySchemeId, new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Name = "X-Cron-Key",
                Type = SecuritySchemeType.ApiKey,
                Description = "Shared secret of the scheduler (Cron__ApiKey). Only for scheduled job endpoints."
            });

            // Only endpoints that are not [AllowAnonymous] require the bearer token.
            options.OperationFilter<BearerSecurityRequirementOperationFilter>();

            options.EnableAnnotations();

            // XML documentation comments: summaries, remarks, parameters and <example> values of the resources.
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{builder.Environment.ApplicationName}.xml"), includeControllerXmlComments: true);
            options.SupportNonNullableReferenceTypes();
        });
    }
}
