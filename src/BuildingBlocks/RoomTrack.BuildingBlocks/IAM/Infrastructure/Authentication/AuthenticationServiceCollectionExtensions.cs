using BackendAwRoomTrack.API.IAM.Infrastructure.Tokens.JWT.Configuration;
using BackendAwRoomTrack.API.IAM.Interfaces.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BackendAwRoomTrack.API.IAM.Infrastructure.Authentication;

/// <summary>
///     Authentication and authorization shared by every service: each one validates the access tokens issued by the
///     Identity service itself (same signing key, issuer and audience), so no request needs a round trip to Identity
///     to be authenticated; only the session generation is checked (<see cref="IamJwtBearerEvents"/>).
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    ///     Native ASP.NET Core authentication: JWT bearer tokens signed with <see cref="TokenSettings"/>.
    ///     The settings are validated when the host starts (the app does not boot without a strong secret).
    /// </summary>
    public static IServiceCollection AddIamAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TokenSettings>()
            .Bind(configuration.GetSection(TokenSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IamJwtBearerEvents>();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureIamJwtBearerOptions>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer()
            .AddJwtBearer(IamAuthenticationSchemes.MfaChallenge);
        return services;
    }

    /// <summary>
    ///     Native ASP.NET Core authorization: every endpoint requires an authenticated user unless it is
    ///     explicitly marked <see cref="AllowAnonymousAttribute"/>; capabilities are the policies of
    ///     <see cref="Policies"/>.
    /// </summary>
    public static IServiceCollection AddIamAuthorization(this IServiceCollection services)
    {
        var authenticatedUser = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .Build();

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(authenticatedUser)
            .SetFallbackPolicy(authenticatedUser)
            .AddRoomTrackPolicies()
            // The second-factor endpoints only accept the matching challenge token.
            .AddPolicy(Policies.EnrollSecondFactor, policy => policy
                .AddAuthenticationSchemes(IamAuthenticationSchemes.MfaChallenge)
                .RequireAuthenticatedUser()
                .RequireClaim(IamClaimTypes.MfaChallenge, IamClaimTypes.MfaChallengeEnrollment))
            .AddPolicy(Policies.VerifySecondFactor, policy => policy
                .AddAuthenticationSchemes(IamAuthenticationSchemes.MfaChallenge)
                .RequireAuthenticatedUser()
                .RequireClaim(IamClaimTypes.MfaChallenge, IamClaimTypes.MfaChallengeVerification));

        return services;
    }
}
