using Scalar.AspNetCore;

namespace BackendAwRoomTrack.API.Shared.Infrastructure.Documentation.OpenApi.Configuration.Extensions;

/// <summary>
/// Extension methods for configuring the API documentation and CORS middleware in the application pipeline.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    ///     Public API documentation in every environment:
    ///     <list type="bullet">
    ///         <item><c>/scalar</c>: interactive API reference (operations grouped by tag, request/response schemas
    ///         and examples, "try it" with the bearer token, code samples in several languages);</item>
    ///         <item><c>/swagger</c>: Swagger UI; <c>/swagger/v1/swagger.json</c>: the OpenAPI document;</item>
    ///         <item><c>/</c> redirects to <c>/scalar</c>.</item>
    ///     </list>
    /// </summary>
    public static void MapApiDocumentation(this WebApplication app)
    {
        app.MapSwagger().AllowAnonymous();

        app.MapScalarApiReference("/scalar", options => options
                .WithTitle("RoomTrack API reference")
                .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json")
                .AddPreferredSecuritySchemes(BearerSecurityRequirementOperationFilter.SchemeId)
                .WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.Fetch)
                .DisableTelemetry()
                .DisableAgent())
            .AllowAnonymous();

        app.MapGet("/", () => Results.Redirect("/scalar")).AllowAnonymous().ExcludeFromDescription();
    }

    /// <summary>Swagger UI at <c>/swagger</c> (static middleware before authentication, so it stays public).</summary>
    public static void UseOpenApiConfiguration(this WebApplication app)
    {
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", $"{app.Environment.ApplicationName} v1");
            c.RoutePrefix = "swagger";
        });
    }
}
