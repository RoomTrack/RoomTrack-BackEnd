using Microsoft.OpenApi.Models;
using RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;

namespace RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration.Extensions;

/// <summary>
/// Extension methods for configuring the OpenAPI documentation of a service (CORS is handled by the API gateway).
/// </summary>
public static class WebApplicationBuilderExtensions
{
    private const string CommonDescription = """


        **Gateway.** Every service is published behind the YARP API gateway, which routes `/api/v1/...` to it; this
        document describes only the operations of this service.

        **Authentication.** Sign in with `POST /api/v1/authentication/sign-in` (Identity service) and send the returned
        `token` as `Authorization: Bearer <token>`. Access tokens last 30 minutes.

        **Errors.** Every error is an RFC 7807 ProblemDetails (`application/problem+json`): read `detail`, branch on the
        stable `code` (e.g. `cleaning_task.checklist_incomplete`); validation errors list each invalid field in `errors`.
        """;

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
                    Description = ServiceDescriptions.For(serviceTitle) + CommonDescription,
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

            // Request and response JSON examples of the operations ([OpenApiRequestExample]/[OpenApiResponseExample]).
            options.OperationFilter<OpenApiExamplesOperationFilter>();

            // XML documentation comments: summaries, remarks, parameters and <example> values of the resources.
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{builder.Environment.ApplicationName}.xml"), includeControllerXmlComments: true);
            options.SupportNonNullableReferenceTypes();
        });
    }
}
