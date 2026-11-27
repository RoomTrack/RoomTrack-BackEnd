using System.Text.Json;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RoomTrack.Shared.Infrastructure.Documentation.OpenApi.Configuration;

/// <summary>
///     A JSON example of the request body of an operation, shown by Swagger UI and exported in the OpenAPI document
///     (<c>requestBody.content["application/json"].example</c>).
/// </summary>
/// <param name="json">The body, as JSON.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OpenApiRequestExampleAttribute(string json) : Attribute
{
    public string Json { get; } = json;
}

/// <summary>
///     A JSON example of a response of an operation for a status code
///     (<c>responses[status].content["application/json"].example</c>).
/// </summary>
/// <param name="statusCode">The HTTP status of the response.</param>
/// <param name="json">The body, as JSON.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class OpenApiResponseExampleAttribute(int statusCode, string json) : Attribute
{
    public int StatusCode { get; } = statusCode;
    public string Json { get; } = json;
}

/// <summary>
///     Writes the <see cref="OpenApiRequestExampleAttribute"/> and <see cref="OpenApiResponseExampleAttribute"/> of each
///     action into its operation, and gives every error response without its own example the RFC 7807 ProblemDetails
///     the API returns, and builds the body of every other request and response from its schema
///     (<see cref="SchemaExamples"/>), so each documented request and response has a concrete body.
/// </summary>
public class OpenApiExamplesOperationFilter : IOperationFilter
{
    private static readonly Dictionary<int, string> ProblemExamples = new()
    {
        [400] = """
            { "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1", "title": "One or more validation errors occurred.",
              "status": 400, "code": "validation.failed",
              "errors": { "title": ["The Title field is required."] } }
            """,
        [401] = """{ "title": "Unauthorized", "status": 401, "code": "auth.unauthenticated", "detail": "Send a valid bearer token." }""",
        [403] = """{ "title": "Forbidden", "status": 403, "code": "auth.forbidden", "detail": "You cannot access resources outside your hotel." }""",
        [404] = """{ "title": "Not Found", "status": 404, "code": "resource.not_found", "detail": "The resource was not found." }""",
        [409] = """{ "title": "Conflict", "status": 409, "code": "request.conflict", "detail": "The current state does not allow the operation." }"""
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attributes = context.MethodInfo.GetCustomAttributes(inherit: true);

        if (attributes.OfType<OpenApiRequestExampleAttribute>().FirstOrDefault() is { } request && operation.RequestBody is not null)
            foreach (var media in operation.RequestBody.Content.Values)
                media.Example = Parse(request.Json, context);

        foreach (var response in attributes.OfType<OpenApiResponseExampleAttribute>())
            if (operation.Responses.TryGetValue(response.StatusCode.ToString(), out var documented))
                foreach (var media in documented.Content.Values)
                    media.Example = Parse(response.Json, context);

        foreach (var (status, documented) in operation.Responses)
            if (int.TryParse(status, out var code) && ProblemExamples.TryGetValue(code, out var problem))
                foreach (var media in documented.Content.Values.Where(media => media.Example is null))
                    media.Example = Parse(problem, context);

        // Whatever has no hand-written example gets one built from its schema (and the <example> of its properties).
        if (operation.RequestBody is not null)
            foreach (var media in operation.RequestBody.Content.Values.Where(media => media.Example is null))
                media.Example = SchemaExamples.Build(media.Schema, context.SchemaRepository);
        foreach (var documented in operation.Responses.Values)
            foreach (var media in documented.Content.Values.Where(media => media.Example is null))
                media.Example = SchemaExamples.Build(media.Schema, context.SchemaRepository);
    }

    private static IOpenApiAny Parse(string json, OperationFilterContext context)
    {
        try
        {
            return OpenApiAnyFactory.CreateFromJson(json);
        }
        catch (JsonException e)
        {
            throw new InvalidOperationException(
                $"Invalid OpenAPI example JSON on {context.MethodInfo.DeclaringType?.Name}.{context.MethodInfo.Name}: {e.Message}", e);
        }
    }
}
