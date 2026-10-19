using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using BackendAwRoomTrack.API.Shared.Infrastructure.Interfaces.ASP.ExceptionHandling;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BackendAwRoomTrack.API.Shared.Infrastructure.Internal;

/// <summary>
///     Authenticates the calls between services (<c>/internal/v1/*</c>) by the shared key of the
///     <see cref="HeaderName"/> header. The API gateway never routes <c>/internal</c>, so these endpoints are only
///     reachable inside the services' network; the key is a second barrier (defense in depth).
/// </summary>
public class InternalApiAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<InternalApiSettings> settings,
    IProblemDetailsService problemDetailsService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "InternalApiKey";
    public const string HeaderName = "X-Internal-Key";
    public const string ScopeClaim = "scope";
    public const string InternalScope = "internal";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var presented) || string.IsNullOrEmpty(presented))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!KeysMatch(presented.ToString(), settings.Value.Key))
            return Task.FromResult(AuthenticateResult.Fail("Invalid internal API key."));

        var identity = new ClaimsIdentity(
            [new Claim(ScopeClaim, InternalScope), new Claim(ClaimTypes.Name, "internal-service")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = $"A valid {HeaderName} header is required to call the internal API.",
                Extensions = { [ProblemCodes.CodeExtension] = ProblemCodes.InternalKeyInvalid }
            }
        });
    }

    private static bool KeysMatch(string presented, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(presented)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}

/// <summary>Shared key of the calls between services (<c>InternalApi</c> section).</summary>
public class InternalApiSettings
{
    public const string SectionName = "InternalApi";

    [Required(ErrorMessage = "InternalApi:Key is not configured. Set 'InternalApi__Key' (at least 32 random characters, the same in every service).")]
    [MinLength(32, ErrorMessage = "InternalApi:Key must have at least 32 characters.")]
    public string Key { get; set; } = string.Empty;
}
