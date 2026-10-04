using System.Net;
using System.Text.Json;
using BackendAwRoomTrack.API.Shared.Infrastructure.Interfaces.ASP.ExceptionHandling;
using BackendAwRoomTrack.Domain.Shared.Domain.Model.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace BackendAwRoomTrack.API.Shared.Infrastructure.Internal;

/// <summary>
///     Base of the HTTP adapters that implement an ACL port by calling the owning service's internal API.
/// </summary>
/// <remarks>
///     The remote errors come back as the same exceptions the in-process facade threw: a ProblemDetails with status
///     400/403/404/409/410 becomes the matching <see cref="DomainException"/> with its stable code and message, so
///     the caller's error handling (and the client's response) does not change. When the other service cannot be
///     reached or fails, <see cref="DependencyUnavailableException"/> answers 503.
/// </remarks>
public abstract class InternalServiceClient(HttpClient httpClient, string serviceName)
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>GET; null when the resource does not exist (404).</summary>
    protected async Task<T?> GetOrDefaultAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(() => httpClient.GetAsync(path, cancellationToken));
        if (response.StatusCode == HttpStatusCode.NotFound) return default;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    /// <summary>GET of a resource that must exist.</summary>
    protected async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(() => httpClient.GetAsync(path, cancellationToken));
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken))!;
    }

    /// <summary>POST with a JSON body; returns the JSON answer, or null for 204/404.</summary>
    protected async Task<TResponse?> PostAsync<TResponse>(string path, object? body, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(() => httpClient.PostAsJsonAsync(path, body, JsonOptions, cancellationToken));
        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound) return default;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    /// <summary>POST with a JSON body and no answer.</summary>
    protected async Task PostAsync(string path, object? body, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(() => httpClient.PostAsJsonAsync(path, body, JsonOptions, cancellationToken));
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            return await send();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                              or Polly.CircuitBreaker.BrokenCircuitException or Polly.Timeout.TimeoutRejectedException)
        {
            throw new DependencyUnavailableException(serviceName, exception);
        }
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        ProblemDetails? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions, cancellationToken);
        }
        catch (Exception)
        {
            // Not a ProblemDetails body: handled below by status code.
        }

        var code = problem?.Extensions.TryGetValue(ProblemCodes.CodeExtension, out var value) == true
            ? value?.ToString()
            : null;
        var detail = problem?.Detail ?? $"The {serviceName} service answered {(int)response.StatusCode}.";

        throw (response.StatusCode, code) switch
        {
            (HttpStatusCode.BadRequest, { } c) => new DomainValidationException(c, detail),
            (HttpStatusCode.Forbidden, { } c) => new OperationNotAllowedException(c, detail),
            (HttpStatusCode.NotFound, { } c) => new RemoteEntityNotFoundException(c, detail),
            (HttpStatusCode.Conflict, { } c) => new BusinessRuleViolationException(c, detail),
            (HttpStatusCode.Gone, { } c) => new ResourceExpiredException(c, detail),
            _ => new DependencyUnavailableException(serviceName,
                new HttpRequestException($"{serviceName} answered {(int)response.StatusCode}: {detail}"))
        };
    }
}

/// <summary>A resource of another service was not found (same code and message as in that service).</summary>
public class RemoteEntityNotFoundException(string code, string message) : EntityNotFoundException(code, message);

/// <summary>Another service this request depends on cannot be reached or failed (503).</summary>
public class DependencyUnavailableException(string serviceName, Exception innerException)
    : Exception($"The {serviceName} service is not available right now. Try again later.", innerException)
{
    public string ServiceName { get; } = serviceName;
}
